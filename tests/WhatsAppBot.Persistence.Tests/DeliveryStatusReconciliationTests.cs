using System.Data;
using Microsoft.Data.SqlClient;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class DeliveryStatusReconciliationTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task FailedThenLaterConfirmedDeliveryPreservesFailureEvidence()
    {
        var tenantId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var phoneNumberId = $"delivery-{Guid.NewGuid():N}";
        var providerMessageId = $"wamid.delivery-{Guid.NewGuid():N}";
        var statusBase = DateTimeOffset.UtcNow.AddMinutes(-3);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateOutboundMessageAsync(
                connection, tenantId, integrationId, phoneNumberId, providerMessageId);

            var failed = await ApplyStatusAsync(
                connection, phoneNumberId, providerMessageId, "failed", statusBase);
            var failureEvidence = await ReadMessageStateAsync(connection, providerMessageId);
            Assert.Equal("MetaDeliveryFailed", failureEvidence.FailureCode);
            var delivered = await ApplyStatusAsync(
                connection, phoneNumberId, providerMessageId, "delivered", statusBase.AddSeconds(1));
            var read = await ApplyStatusAsync(
                connection, phoneNumberId, providerMessageId, "read", statusBase.AddSeconds(2));

            Assert.True(failed.Applied);
            Assert.Equal("Failed", failed.DeliveryState);
            Assert.True(delivered.Applied);
            Assert.Equal("Delivered", delivered.DeliveryState);
            Assert.True(read.Applied);
            Assert.Equal("Read", read.DeliveryState);

            var evidence = await ReadMessageStateAsync(connection, providerMessageId);
            Assert.Equal("Read", evidence.DeliveryState);
            Assert.Equal("MetaDeliveryFailed", evidence.FailureCode);
        }
        finally
        {
            await DeleteTestDataAsync(connection, tenantId, integrationId, providerMessageId);
        }
    }

    [Fact]
    public async Task OlderFailureDoesNotDegradeAConfirmedSentState()
    {
        var tenantId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var phoneNumberId = $"delivery-{Guid.NewGuid():N}";
        var providerMessageId = $"wamid.delivery-{Guid.NewGuid():N}";
        var statusAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateOutboundMessageAsync(
                connection, tenantId, integrationId, phoneNumberId, providerMessageId,
                deliveryState: "Sent", statusAtUtc: statusAt);

            var result = await ApplyStatusAsync(
                connection, phoneNumberId, providerMessageId, "failed", statusAt.AddSeconds(-1));

            Assert.False(result.Applied);
            Assert.Equal("Sent", result.DeliveryState);
            var evidence = await ReadMessageStateAsync(connection, providerMessageId);
            Assert.Equal("Sent", evidence.DeliveryState);
            Assert.Null(evidence.FailureCode);
        }
        finally
        {
            await DeleteTestDataAsync(connection, tenantId, integrationId, providerMessageId);
        }
    }

    private static async Task CreateOutboundMessageAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid integrationId,
        string phoneNumberId,
        string providerMessageId,
        string deliveryState = "Pending",
        DateTimeOffset? statusAtUtc = null)
    {
        await using var command = new SqlCommand(
            """
            INSERT dbo.Tenant (TenantId, LegalName, IsActive)
            VALUES (@TenantId, N'Delivery status test', 1);
            INSERT dbo.WhatsAppIntegration
                (IntegrationId, TenantId, WabaId, PhoneNumberId, BusinessPhoneNumber,
                 ConnectionState, ConnectedAtUtc, IsActive)
            VALUES
                (@IntegrationId, @TenantId, N'test-waba', @PhoneNumberId, N'+15550000000',
                 N'Connected', SYSUTCDATETIME(), 1);
            INSERT dbo.Contact (IntegrationId, TenantId, WhatsAppUserId, DisplayName)
            VALUES (@IntegrationId, @TenantId, @WhatsAppUserId, N'Delivery status test');
            DECLARE @ContactId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.Conversation (IntegrationId, ContactId, TenantId, BasicStatus)
            VALUES (@IntegrationId, @ContactId, @TenantId, N'Open');
            DECLARE @ConversationId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.Message
                (IntegrationId, ConversationId, ProviderMessageId, Direction, MessageType,
                 ContentText, ReplySource, ProcessingState, DeliveryState,
                 ProviderTimestampUtc, DeliveryStatusAtUtc, TenantId, ActorType, Origin)
            VALUES
                (@IntegrationId, @ConversationId, @ProviderMessageId, N'Outbound', N'Text',
                 N'Delivery status test', N'AI', N'Completed', @DeliveryState,
                 @StatusAtUtc, @StatusAtUtc, @TenantId, N'Bot', N'MetaCloudApi');
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64) { Value = phoneNumberId });
        command.Parameters.Add(new SqlParameter("@WhatsAppUserId", SqlDbType.NVarChar, 64)
            { Value = $"user-{Guid.NewGuid():N}" });
        command.Parameters.Add(new SqlParameter("@ProviderMessageId", SqlDbType.NVarChar, 256)
            { Value = providerMessageId });
        command.Parameters.Add(new SqlParameter("@DeliveryState", SqlDbType.NVarChar, 24)
            { Value = deliveryState });
        command.Parameters.Add(new SqlParameter("@StatusAtUtc", SqlDbType.DateTime2)
            { Scale = 3, Value = (object?)statusAtUtc?.UtcDateTime ?? DBNull.Value });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(bool Applied, string? DeliveryState)> ApplyStatusAsync(
        SqlConnection connection,
        string phoneNumberId,
        string providerMessageId,
        string status,
        DateTimeOffset timestamp)
    {
        await using var command = new SqlCommand("dbo.sp_Message_ApplyDeliveryStatus", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64) { Value = phoneNumberId });
        command.Parameters.Add(new SqlParameter("@ProviderMessageId", SqlDbType.NVarChar, 256)
            { Value = providerMessageId });
        command.Parameters.Add(new SqlParameter("@DeliveryStatus", SqlDbType.NVarChar, 24) { Value = status });
        command.Parameters.Add(new SqlParameter("@ProviderTimestampUtc", SqlDbType.DateTime2)
            { Scale = 3, Value = timestamp.UtcDateTime });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (
            reader.GetBoolean(reader.GetOrdinal("Applied")),
            reader.IsDBNull(reader.GetOrdinal("DeliveryState"))
                ? null
                : reader.GetString(reader.GetOrdinal("DeliveryState")));
    }

    private static async Task<(string DeliveryState, string? FailureCode)> ReadMessageStateAsync(
        SqlConnection connection, string providerMessageId)
    {
        await using var command = new SqlCommand(
            "SELECT DeliveryState, FailureCode FROM dbo.Message WHERE ProviderMessageId = @ProviderMessageId;",
            connection);
        command.Parameters.Add(new SqlParameter("@ProviderMessageId", SqlDbType.NVarChar, 256)
            { Value = providerMessageId });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (
            reader.GetString(reader.GetOrdinal("DeliveryState")),
            reader.IsDBNull(reader.GetOrdinal("FailureCode"))
                ? null
                : reader.GetString(reader.GetOrdinal("FailureCode")));
    }

    private static async Task DeleteTestDataAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid integrationId,
        string providerMessageId)
    {
        await using var command = new SqlCommand(
            """
            DECLARE @ConversationId bigint =
                (SELECT TOP (1) ConversationId FROM dbo.Message WHERE ProviderMessageId = @ProviderMessageId);
            DECLARE @ContactId bigint =
                (SELECT TOP (1) ContactId FROM dbo.Conversation WHERE ConversationId = @ConversationId);
            DELETE dbo.Message WHERE ProviderMessageId = @ProviderMessageId;
            IF @ConversationId IS NOT NULL DELETE dbo.Conversation WHERE ConversationId = @ConversationId;
            IF @ContactId IS NOT NULL DELETE dbo.Contact WHERE ContactId = @ContactId;
            DELETE dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId;
            DELETE dbo.Tenant WHERE TenantId = @TenantId;
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@ProviderMessageId", SqlDbType.NVarChar, 256)
            { Value = providerMessageId });
        await command.ExecuteNonQueryAsync();
    }
}
