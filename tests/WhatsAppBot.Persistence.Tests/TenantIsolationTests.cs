using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Persistence.Repositories;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class TenantIsolationTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task TenantScopedRepositoriesCannotReadOrMutateForeignRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var integrationA = Guid.NewGuid();
        var integrationB = Guid.NewGuid();
        var phoneNumberB = $"tenant-b-phone-{Guid.NewGuid():N}";
        var providerMessageIdB = $"tenant-b-message-{Guid.NewGuid():N}";
        var questionB = $"tenant-b-response-{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await ExecuteAsync(
                connection,
                """
                INSERT dbo.Tenant (TenantId, LegalName, IsActive)
                VALUES (@TenantA, N'Tenant A', 1), (@TenantB, N'Tenant B', 1);
                INSERT dbo.WhatsAppIntegration
                    (IntegrationId, TenantId, WabaId, PhoneNumberId, BusinessPhoneNumber,
                     ConnectionState, OnboardingType, IsActive)
                VALUES
                    (@IntegrationA, @TenantA, N'waba-a', N'tenant-a-phone', N'+15550000001',
                     N'Connected', N'Standard', 1),
                    (@IntegrationB, @TenantB, N'waba-b', @PhoneNumberB, N'+15550000002',
                     N'Connected', N'Standard', 1);
                INSERT dbo.BotConfiguration (IntegrationId, TenantId, IsBotEnabled, ReplyMode)
                VALUES (@IntegrationA, @TenantA, 1, N'FaqThenAi'), (@IntegrationB, @TenantB, 1, N'FaqThenAi');
                INSERT dbo.FrequentResponse
                    (IntegrationId, TenantId, QuestionOrIntent, AnswerText, Priority, IsActive)
                VALUES (@IntegrationB, @TenantB, @QuestionB, N'Tenant B answer', 10, 1);
                DECLARE @FrequentResponseId bigint = CONVERT(bigint, SCOPE_IDENTITY());
                INSERT dbo.FrequentResponseExpression
                    (FrequentResponseId, ExpressionText, NormalizedExpression)
                VALUES (@FrequentResponseId, N'tenant b phrase', N'tenant b phrase');
                DECLARE @ContactB bigint;
                INSERT dbo.Contact (IntegrationId, TenantId, WhatsAppUserId, DisplayName)
                VALUES (@IntegrationB, @TenantB, N'tenant-b-user', N'Tenant B contact');
                SET @ContactB = CONVERT(bigint, SCOPE_IDENTITY());
                DECLARE @ConversationB bigint;
                INSERT dbo.Conversation (IntegrationId, ContactId, TenantId, LastMessageAtUtc)
                VALUES (@IntegrationB, @ContactB, @TenantB, SYSUTCDATETIME());
                SET @ConversationB = CONVERT(bigint, SCOPE_IDENTITY());
                INSERT dbo.Message
                    (IntegrationId, ConversationId, ProviderMessageId, Direction, MessageType,
                     ContentText, ProcessingState, TenantId, ActorType, Origin, ProviderTimestampUtc)
                VALUES
                    (@IntegrationB, @ConversationB, @ProviderMessageIdB, N'Inbound', N'Audio',
                     N'Tenant B message', N'Completed', @TenantB, N'Customer', N'MetaCloudApi', SYSUTCDATETIME());
                DECLARE @MessageB bigint = CONVERT(bigint, SCOPE_IDENTITY());
                INSERT dbo.MediaAttachment
                    (MessageId, TenantId, MediaKind, MimeType, SizeBytes, StorageReference,
                     AvailabilityState, ExpiresAtUtc)
                VALUES
                    (@MessageB, @TenantB, N'Audio', N'audio/ogg', 12, N'private/tenant-b',
                     N'Available', DATEADD(day, 1, SYSUTCDATETIME()));
                """,
                ("@TenantA", SqlDbType.UniqueIdentifier, tenantA),
                ("@TenantB", SqlDbType.UniqueIdentifier, tenantB),
                ("@IntegrationA", SqlDbType.UniqueIdentifier, integrationA),
                ("@IntegrationB", SqlDbType.UniqueIdentifier, integrationB),
                ("@PhoneNumberB", SqlDbType.NVarChar, phoneNumberB),
                ("@ProviderMessageIdB", SqlDbType.NVarChar, providerMessageIdB),
                ("@QuestionB", SqlDbType.NVarChar, questionB));

            var configuration = await fixture.ConfigurationRepository.GetCurrentAsync(
                tenantA, CancellationToken.None);
            Assert.Equal(integrationA, configuration.IntegrationId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ConfigurationRepository.GetForIntegrationAsync(
                tenantA, integrationB, CancellationToken.None));

            var integrationRepository = new WhatsAppIntegrationRepository(
                fixture.Executor, new UnusedTenantContext());
            await Assert.ThrowsAsync<InvalidOperationException>(() => integrationRepository.GetByPhoneNumberIdAsync(
                tenantA, integrationB, phoneNumberB, CancellationToken.None));

            var conversationId = await ReadScalarAsync(
                connection,
                "SELECT ConversationId FROM dbo.Conversation WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;",
                ("@TenantId", SqlDbType.UniqueIdentifier, tenantB),
                ("@IntegrationId", SqlDbType.UniqueIdentifier, integrationB));
            var messageId = await ReadScalarAsync(
                connection,
                "SELECT MessageId FROM dbo.Message WHERE TenantId = @TenantId AND ProviderMessageId = @ProviderMessageId;",
                ("@TenantId", SqlDbType.UniqueIdentifier, tenantB),
                ("@ProviderMessageId", SqlDbType.NVarChar, providerMessageIdB));
            var frequentResponseId = await ReadScalarAsync(
                connection,
                "SELECT FrequentResponseId FROM dbo.FrequentResponse WHERE TenantId = @TenantId AND QuestionOrIntent = @Question;",
                ("@TenantId", SqlDbType.UniqueIdentifier, tenantB),
                ("@Question", SqlDbType.NVarChar, questionB));

            var conversationRepository = new ConversationRepository(fixture.Executor);
            var conversations = await conversationRepository.ListAsync(
                tenantA, integrationB, 1, 25, CancellationToken.None);
            Assert.Empty(conversations.Items);
            Assert.Equal(0, conversations.TotalCount);

            var messageRepository = new MessageRepository(fixture.Executor);
            Assert.Null(await messageRepository.GetHistoryAsync(
                tenantA, integrationB, conversationId, null, 25, CancellationToken.None));
            var delivery = await messageRepository.ApplyDeliveryStatusAsync(
                tenantA,
                integrationB,
                new NormalizedMetaEvent(
                    $"status:{Guid.NewGuid():N}",
                    MetaWebhookEventTypes.DeliveryStatus,
                    phoneNumberB,
                    "waba-b",
                    null,
                    null,
                    providerMessageIdB,
                    null,
                    DateTimeOffset.UtcNow,
                    "delivered"),
                CancellationToken.None);
            Assert.False(delivery.Found);
            Assert.False(delivery.Applied);
            await messageRepository.SetProcessingOutcomeAsync(
                tenantA, messageId, ProcessingState.Failed, "TenantLeak", CancellationToken.None);

            var frequentResponses = new FrequentResponseRepository(fixture.Executor);
            Assert.Null(await frequentResponses.GetAsync(
                tenantA, integrationB, frequentResponseId, CancellationToken.None));

            var mediaRepository = new MediaRepository(fixture.Executor);
            Assert.Null(await mediaRepository.GetForAccessAsync(tenantA, messageId, CancellationToken.None));
            await mediaRepository.MarkDeletedAsync(tenantA, messageId, CancellationToken.None);

            Assert.Equal(
                "Completed",
                await ReadStringAsync(connection, "SELECT ProcessingState FROM dbo.Message WHERE MessageId = @MessageId;",
                    ("@MessageId", SqlDbType.BigInt, messageId)));
            Assert.Equal(
                "Available",
                await ReadStringAsync(connection,
                    "SELECT AvailabilityState FROM dbo.MediaAttachment WHERE MessageId = @MessageId;",
                    ("@MessageId", SqlDbType.BigInt, messageId)));
        }
        finally
        {
            await ExecuteAsync(
                connection,
                """
                DELETE dbo.FrequentResponseExpression
                WHERE FrequentResponseId IN
                    (SELECT FrequentResponseId FROM dbo.FrequentResponse WHERE TenantId IN (@TenantA, @TenantB));
                DELETE dbo.MediaAttachment WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.Transcript WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.MessageOutbox WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.Message WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.ConversationAuditEvent WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.Conversation WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.Contact WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.FrequentResponse WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.BotConfiguration WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.WhatsAppIntegration WHERE TenantId IN (@TenantA, @TenantB);
                DELETE dbo.Tenant WHERE TenantId IN (@TenantA, @TenantB);
                """,
                ("@TenantA", SqlDbType.UniqueIdentifier, tenantA),
                ("@TenantB", SqlDbType.UniqueIdentifier, tenantB));
        }
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        params (string Name, SqlDbType Type, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ReadScalarAsync(
        SqlConnection connection,
        string sql,
        params (string Name, SqlDbType Type, object Value)[] parameters) =>
        Convert.ToInt64(await ReadObjectAsync(connection, sql, parameters));

    private static async Task<string> ReadStringAsync(
        SqlConnection connection,
        string sql,
        params (string Name, SqlDbType Type, object Value)[] parameters) =>
        Convert.ToString(await ReadObjectAsync(connection, sql, parameters))
        ?? throw new InvalidOperationException("The test query returned no string value.");

    private static async Task<object> ReadObjectAsync(
        SqlConnection connection,
        string sql,
        params (string Name, SqlDbType Type, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The test query returned no value.");
    }

    private static void AddParameters(
        SqlCommand command,
        IEnumerable<(string Name, SqlDbType Type, object Value)> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.Name, parameter.Type) { Value = parameter.Value });
        }
    }

    private sealed class UnusedTenantContext : ITenantContext
    {
        public TenantScope? Current => null;

        public Task<TenantScope> GetRequiredAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This test uses an explicitly supplied tenant.");
    }
}
