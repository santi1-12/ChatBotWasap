using System.Data;
using Microsoft.Data.SqlClient;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class ConversationReopeningTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task ClosedConversation_ReopensInPlaceWithPersistedBotDecisionAndHistory()
    {
        var tenantAndIntegration = await GetActiveTenantAndIntegrationAsync();
        var contactId = 0L;
        var conversationId = 0L;
        var historyMessageId = 0L;
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            (contactId, conversationId, historyMessageId) = await CreateClosedConversationAsync(
                connection, tenantAndIntegration.TenantId, tenantAndIntegration.IntegrationId);
            var originalVersion = await ReadVersionAsync(connection, conversationId);

            var reopenedAsBot = await GetOrCreateAsync(
                connection,
                tenantAndIntegration.TenantId,
                tenantAndIntegration.IntegrationId,
                contactId,
                true);
            var botState = await ReadStateAsync(connection, conversationId);

            Assert.Equal(conversationId, reopenedAsBot);
            Assert.Equal("Abierta", botState.LifecycleStatus);
            Assert.Equal("Bot", botState.AttentionMode);
            Assert.Equal(1, botState.HistoryCount);
            Assert.NotEqual(originalVersion, botState.Version);

            await CloseConversationAsync(connection, conversationId);
            var reopenedAsHuman = await GetOrCreateAsync(
                connection,
                tenantAndIntegration.TenantId,
                tenantAndIntegration.IntegrationId,
                contactId,
                false);
            var humanState = await ReadStateAsync(connection, conversationId);

            Assert.Equal(conversationId, reopenedAsHuman);
            Assert.Equal("Abierta", humanState.LifecycleStatus);
            Assert.Equal("Humano", humanState.AttentionMode);
            Assert.Equal(1, humanState.HistoryCount);
        }
        finally
        {
            await DeleteConversationAsync(connection, contactId, conversationId, historyMessageId);
        }
    }

    [Fact]
    public async Task ConversationVersion_IsSqlServerRowversion()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TYPE_NAME(system_type_id), system_type_id FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Conversation') AND name = N'Version';",
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("timestamp", reader.GetString(0));
        Assert.Equal(189, reader.GetByte(1));
    }

    private async Task<(Guid TenantId, Guid IntegrationId)> GetActiveTenantAndIntegrationAsync()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TOP (1) TenantId, IntegrationId FROM dbo.WhatsAppIntegration WHERE IsActive = 1 AND TenantId IS NOT NULL ORDER BY CreatedAtUtc, IntegrationId;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("An active Tenant integration is required for the test.");
        }

        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<(long ContactId, long ConversationId, long MessageId)> CreateClosedConversationAsync(
        SqlConnection connection, Guid tenantId, Guid integrationId)
    {
        await using var command = new SqlCommand(
            """
            INSERT dbo.Contact (IntegrationId, TenantId, WhatsAppUserId, DisplayName)
            VALUES (@IntegrationId, @TenantId, @WhatsAppUserId, N'T108 reopening test');
            DECLARE @ContactId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.Conversation
                (TenantId, IntegrationId, ContactId, BasicStatus, LifecycleStatus, AttentionMode,
                 LastMessageAtUtc, ClosedAtUtc)
            VALUES
                (@TenantId, @IntegrationId, @ContactId, N'Closed', N'Cerrada', N'Humano',
                 DATEADD(minute, -5, SYSUTCDATETIME()), SYSUTCDATETIME());
            DECLARE @ConversationId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.Message
                (IntegrationId, ConversationId, Direction, MessageType, ContentText,
                 ProcessingState, ProviderTimestampUtc, TenantId, ActorType, Origin)
            VALUES
                (@IntegrationId, @ConversationId, N'Inbound', N'Text', N'history',
                 N'Completed', DATEADD(minute, -5, SYSUTCDATETIME()), @TenantId,
                 N'Customer', N'MetaCloudApi');
            SELECT @ContactId, @ConversationId, CONVERT(bigint, SCOPE_IDENTITY());
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@WhatsAppUserId", SqlDbType.NVarChar, 64)
            { Value = $"t108-{Guid.NewGuid():N}" });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private static async Task<long> GetOrCreateAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid integrationId,
        long contactId,
        bool isBotEnabled)
    {
        await using var command = new SqlCommand("dbo.sp_Conversation_GetOrCreate", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@ContactId", SqlDbType.BigInt) { Value = contactId });
        command.Parameters.Add(new SqlParameter("@LastMessageAtUtc", SqlDbType.DateTime2)
        {
            Scale = 3,
            Value = DateTime.UtcNow
        });
        command.Parameters.Add(new SqlParameter("@IsBotEnabled", SqlDbType.Bit) { Value = isBotEnabled });
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task CloseConversationAsync(SqlConnection connection, long conversationId)
    {
        await using var command = new SqlCommand(
            "UPDATE dbo.Conversation SET BasicStatus = N'Closed', LifecycleStatus = N'Cerrada', AttentionMode = N'Humano', ClosedAtUtc = SYSUTCDATETIME() WHERE ConversationId = @ConversationId;",
            connection);
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(string LifecycleStatus, string AttentionMode, int HistoryCount, byte[] Version)> ReadStateAsync(
        SqlConnection connection, long conversationId)
    {
        await using var command = new SqlCommand(
            "SELECT LifecycleStatus, AttentionMode, (SELECT COUNT(*) FROM dbo.Message WHERE ConversationId = conversation.ConversationId), CONVERT(binary(8), Version) FROM dbo.Conversation AS conversation WHERE ConversationId = @ConversationId;",
            connection);
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1), reader.GetInt32(2), (byte[])reader[3]);
    }

    private static async Task<byte[]> ReadVersionAsync(SqlConnection connection, long conversationId)
    {
        await using var command = new SqlCommand(
            "SELECT CONVERT(binary(8), Version) FROM dbo.Conversation WHERE ConversationId = @ConversationId;",
            connection);
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId });
        return (byte[])(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The conversation version is missing."));
    }

    private static async Task DeleteConversationAsync(
        SqlConnection connection, long contactId, long conversationId, long messageId)
    {
        await using var command = new SqlCommand(
            "DELETE dbo.Message WHERE MessageId = @MessageId; DELETE dbo.Conversation WHERE ConversationId = @ConversationId; DELETE dbo.Contact WHERE ContactId = @ContactId;",
            connection);
        command.Parameters.Add(new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId });
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId });
        command.Parameters.Add(new SqlParameter("@ContactId", SqlDbType.BigInt) { Value = contactId });
        await command.ExecuteNonQueryAsync();
    }
}
