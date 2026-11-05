using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Persistence.Repositories;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class CoexistencePersistenceTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task HistoricalAndEchoMessages_AreIdempotentAndNeverCreateOutboxRows()
    {
        var integrationId = Guid.NewGuid();
        var historyKey = $"history:{Guid.NewGuid():N}";
        var echoKey = $"echo:{Guid.NewGuid():N}";
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateInactiveIntegrationAsync(connection, integrationId);
            var contacts = new ContactRepository(fixture.Executor);
            var conversations = new ConversationRepository(fixture.Executor);
            var messages = new MessageRepository(fixture.Executor);
            var contactId = await contacts.UpsertWhatsAppAsync(
                integrationId, fixture.TenantId, "15551234567", "Customer", CancellationToken.None);
            var conversationId = await conversations.GetOrCreateCoexistenceAsync(
                integrationId, fixture.TenantId, contactId, DateTimeOffset.UtcNow, true, false,
                CancellationToken.None);
            var historical = CreateEvent(
                integrationId, MetaWebhookEventTypes.HistoricalMessage, historyKey, true, MessageDirection.Inbound);

            var first = await messages.InsertCoexistenceAsync(
                fixture.TenantId, integrationId, conversationId, historical, CancellationToken.None);
            var duplicate = await messages.InsertCoexistenceAsync(
                fixture.TenantId, integrationId, conversationId, historical, CancellationToken.None);

            Assert.True(first.Inserted);
            Assert.False(duplicate.Inserted);
            Assert.Equal(first.MessageId, duplicate.MessageId);

            var echoConversationId = await conversations.GetOrCreateCoexistenceAsync(
                integrationId, fixture.TenantId, contactId, DateTimeOffset.UtcNow, true, true,
                CancellationToken.None);
            var echo = CreateEvent(
                integrationId, MetaWebhookEventTypes.OutboundAppMessage, echoKey, false, MessageDirection.Outbound);
            var echoResult = await messages.InsertCoexistenceAsync(
                fixture.TenantId, integrationId, echoConversationId, echo, CancellationToken.None);

            Assert.True(echoResult.Inserted);
            Assert.Equal(
                "Humano",
                await ScalarAsync(connection,
                    "SELECT AttentionMode FROM dbo.Conversation WHERE ConversationId = @ConversationId;",
                    new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId }));
            Assert.Equal(
                0,
                Convert.ToInt32(await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM dbo.MessageOutbox WHERE MessageId IN (@HistoryMessageId, @EchoMessageId);",
                    new SqlParameter("@HistoryMessageId", SqlDbType.BigInt) { Value = first.MessageId },
                    new SqlParameter("@EchoMessageId", SqlDbType.BigInt) { Value = echoResult.MessageId })));
        }
        finally
        {
            await DeleteIntegrationDataAsync(connection, integrationId);
        }
    }

    [Fact]
    public async Task HistoryInboxOutcome_UpdatesTenantScopedProgress()
    {
        var eventKey = $"history-event:{Guid.NewGuid():N}";
        var historyKey = $"history-import:{Guid.NewGuid():N}";
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            var active = await GetActiveIntegrationAsync(connection);
            Assert.NotNull(active);
            var webhookEvent = CreateEvent(
                active.Value.IntegrationId,
                MetaWebhookEventTypes.HistoricalMessage,
                eventKey,
                true,
                MessageDirection.Inbound) with
            {
                EventKey = eventKey,
                HistoryImportKey = historyKey,
                HistoryTotalCount = 1,
                PhoneNumberId = active.Value.PhoneNumberId,
                WabaId = active.Value.WabaId,
                TenantId = active.Value.TenantId
            };

            Assert.True(await fixture.WebhookInboxRepository.InsertAsync(webhookEvent, CancellationToken.None));
            await fixture.WebhookInboxRepository.SetStateAsync(
                active.Value.TenantId,
                eventKey,
                WebhookInboxState.Completed,
                null,
                CancellationToken.None,
                HistoryImportOutcomes.Imported);

            await using var command = new SqlCommand(
                "SELECT State, ProcessedCount, ImportedCount FROM dbo.HistoryImportStatus WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND HistoryImportKey = @HistoryImportKey;",
                connection);
            command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = active.Value.TenantId });
            command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = active.Value.IntegrationId });
            command.Parameters.Add(new SqlParameter("@HistoryImportKey", SqlDbType.NVarChar, 256) { Value = historyKey });
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Completed", reader.GetString(0));
            Assert.Equal(1, reader.GetInt32(1));
            Assert.Equal(1, reader.GetInt32(2));
        }
        finally
        {
            await using var cleanup = new SqlCommand(
                "DELETE dbo.HistoryImportStatus WHERE HistoryImportKey = @HistoryImportKey; DELETE dbo.WebhookInboxEvent WHERE EventKey = @EventKey;",
                connection);
            cleanup.Parameters.Add(new SqlParameter("@HistoryImportKey", SqlDbType.NVarChar, 256) { Value = historyKey });
            cleanup.Parameters.Add(new SqlParameter("@EventKey", SqlDbType.NVarChar, 256) { Value = eventKey });
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private NormalizedMetaEvent CreateEvent(
        Guid integrationId,
        string eventType,
        string sourceEventKey,
        bool historical,
        MessageDirection direction) => new(
        sourceEventKey,
        eventType,
        "unused-phone",
        "unused-waba",
        "15551234567",
        "Customer",
        $"wamid:{sourceEventKey}",
        "Hello",
        DateTimeOffset.UtcNow,
        null)
    {
        TenantId = fixture.TenantId,
        IntegrationId = integrationId,
        SourceEventKey = sourceEventKey,
        SourceField = historical ? "history" : "smb_message_echoes",
        HistoryImportKey = historical ? sourceEventKey : null,
        HistoryTotalCount = historical ? 1 : 0,
        IsHistorical = historical,
        Direction = direction,
        ActorType = direction == MessageDirection.Outbound ? ActorType.Admin : ActorType.Customer,
        Origin = historical ? Origin.MetaCloudApi : Origin.FutureWhatsAppBusinessApp,
        MessageType = MessageType.Text
    };

    private async Task CreateInactiveIntegrationAsync(SqlConnection connection, Guid integrationId)
    {
        await using var command = new SqlCommand(
            "INSERT dbo.WhatsAppIntegration (IntegrationId, TenantId, ConnectionState, IsActive, OnboardingType, HistoryImportRequested) VALUES (@IntegrationId, @TenantId, N'NotConnected', 0, N'Coexistence', 1);",
            connection);
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = fixture.TenantId });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(Guid IntegrationId, Guid TenantId, string PhoneNumberId, string WabaId)?>
        GetActiveIntegrationAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) IntegrationId, TenantId, PhoneNumberId, WabaId FROM dbo.WhatsAppIntegration WHERE IsActive = 1 AND ConnectionState = N'Connected' AND PhoneNumberId IS NOT NULL AND WabaId IS NOT NULL;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? (reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3))
            : null;
    }

    private static async Task<object?> ScalarAsync(
        SqlConnection connection, string sql, params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return await command.ExecuteScalarAsync();
    }

    private static async Task DeleteIntegrationDataAsync(SqlConnection connection, Guid integrationId)
    {
        await using var command = new SqlCommand(
            "DELETE dbo.Message WHERE IntegrationId = @IntegrationId; DELETE dbo.Conversation WHERE IntegrationId = @IntegrationId; DELETE dbo.Contact WHERE IntegrationId = @IntegrationId; DELETE dbo.BotConfiguration WHERE IntegrationId = @IntegrationId; DELETE dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId;",
            connection);
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId });
        await command.ExecuteNonQueryAsync();
    }
}
