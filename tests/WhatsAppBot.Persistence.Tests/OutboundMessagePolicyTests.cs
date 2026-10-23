using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class OutboundMessagePolicyTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task ExpiredWindow_DoesNotPersistOutboundMessageOrOutboxRow()
    {
        var integrationId = Guid.NewGuid();
        var contactId = 0L;
        var conversationId = 0L;
        var inboundMessageId = 0L;
        var phoneNumberId = $"test-{Guid.NewGuid():N}";
        var ownsIntegration = false;
        var tenantId = Guid.Empty;
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            if (!await HasActiveIntegrationAsync(connection))
            {
                tenantId = await CreateConnectedTestIntegrationAsync(connection, integrationId, phoneNumberId);
                ownsIntegration = true;
            }
            else
            {
                (integrationId, tenantId) = await GetActiveIntegrationAsync(connection);
            }

            (contactId, conversationId, inboundMessageId) =
                await InsertExpiredInboundAsync(connection, integrationId, tenantId);

            var result = await fixture.OutboxRepository.CreatePendingAsync(
                inboundMessageId, integrationId, conversationId, "Must not be sent",
                ReplySource.FrequentResponse, null, CancellationToken.None);

            Assert.False(result.Inserted);
            Assert.Null(result.MessageId);
            Assert.Equal(MessageOutcomeCodes.MessagingWindowClosed, result.OutcomeCode);
            Assert.Equal(0, await CountOutboundForConversationAsync(connection, conversationId));
        }
        finally
        {
            await DeleteTestDataAsync(
                connection, integrationId, contactId, conversationId, inboundMessageId, ownsIntegration);
        }
    }

    private static async Task<bool> HasActiveIntegrationAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            "SELECT CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.WhatsAppIntegration WHERE IsActive = 1) THEN 1 ELSE 0 END);",
            connection);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<(Guid IntegrationId, Guid TenantId)> GetActiveIntegrationAsync(
        SqlConnection connection)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) IntegrationId, TenantId FROM dbo.WhatsAppIntegration WHERE IsActive = 1;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("The active integration is missing.");
        }

        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<Guid> CreateConnectedTestIntegrationAsync(
        SqlConnection connection, Guid integrationId, string phoneNumberId)
    {
        await using var command = new SqlCommand(
            """
            DECLARE @TenantId uniqueidentifier =
                (SELECT TOP (1) TenantId FROM dbo.Tenant WHERE IsActive = 1 ORDER BY CreatedAtUtc, TenantId);
            IF @TenantId IS NULL THROW 51040, 'A test Tenant is required.', 1;
            INSERT dbo.WhatsAppIntegration
                (IntegrationId, TenantId, WabaId, PhoneNumberId, BusinessPhoneNumber, ConnectionState, IsActive)
            VALUES (@IntegrationId, @TenantId, N'test-waba', @PhoneNumberId, N'+15550000000', N'Connected', 1);
            INSERT dbo.BotConfiguration (IntegrationId, TenantId, IsBotEnabled, ReplyMode)
            VALUES (@IntegrationId, @TenantId, 0, N'FaqThenAi');
            SELECT @TenantId;
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier)
            { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64)
            { Value = phoneNumberId });
        return (Guid)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The test Tenant could not be created."));
    }

    private static async Task<(long ContactId, long ConversationId, long MessageId)> InsertExpiredInboundAsync(
        SqlConnection connection, Guid integrationId, Guid tenantId)
    {
        await using var command = new SqlCommand(
            """
            INSERT dbo.Contact (IntegrationId, TenantId, WhatsAppUserId, DisplayName)
            VALUES (@IntegrationId, @TenantId, @WhatsAppUserId, N'Outbox policy test');
            DECLARE @ContactId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.Conversation (IntegrationId, ContactId, TenantId, BasicStatus)
            VALUES (@IntegrationId, @ContactId, @TenantId, N'Open');
            DECLARE @ConversationId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.Message
                (IntegrationId, ConversationId, ProviderMessageId, Direction, MessageType,
                 ContentText, ProcessingState, ProviderTimestampUtc, TenantId, ActorType, Origin)
            VALUES
                (@IntegrationId, @ConversationId, @ProviderMessageId, N'Inbound', N'Text',
                  N'expired test', N'Received', DATEADD(hour, -25, SYSUTCDATETIME()),
                  @TenantId, N'Customer', N'MetaCloudApi');
            SELECT @ContactId AS ContactId, @ConversationId AS ConversationId,
                   CONVERT(bigint, SCOPE_IDENTITY()) AS MessageId;
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier)
            { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier)
            { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@WhatsAppUserId", SqlDbType.NVarChar, 64)
            { Value = $"test-{Guid.NewGuid():N}" });
        command.Parameters.Add(new SqlParameter("@ProviderMessageId", SqlDbType.NVarChar, 256)
            { Value = $"wamid.{Guid.NewGuid():N}" });
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("The expired inbound test message could not be created.");
        }

        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private static async Task<int> CountOutboundForConversationAsync(SqlConnection connection, long conversationId)
    {
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.Message WHERE ConversationId = @ConversationId AND Direction = N'Outbound';",
            connection);
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId });
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task DeleteTestDataAsync(
        SqlConnection connection,
        Guid integrationId,
        long contactId,
        long conversationId,
        long inboundMessageId,
        bool ownsIntegration)
    {
        await using var command = new SqlCommand(
            """
            IF @InboundMessageId > 0 DELETE dbo.Message WHERE MessageId = @InboundMessageId;
            IF @ConversationId > 0 DELETE dbo.Conversation WHERE ConversationId = @ConversationId;
            IF @ContactId > 0 DELETE dbo.Contact WHERE ContactId = @ContactId;
            IF @OwnsIntegration = 1
            BEGIN
                DELETE dbo.BotConfiguration WHERE IntegrationId = @IntegrationId;
                DELETE dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId;
            END;
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier)
            { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@ContactId", SqlDbType.BigInt) { Value = contactId });
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId });
        command.Parameters.Add(new SqlParameter("@InboundMessageId", SqlDbType.BigInt) { Value = inboundMessageId });
        command.Parameters.Add(new SqlParameter("@OwnsIntegration", SqlDbType.Bit) { Value = ownsIntegration });
        await command.ExecuteNonQueryAsync();
    }
}
