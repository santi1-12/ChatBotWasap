using System.Data;
using Microsoft.Data.SqlClient;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class ConversationConcurrencyTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task StaleConversationAction_ReturnsConflictWithoutOverwritingState()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var context = await CreateConversationAsync(connection);
        try
        {
            var first = await ApplyActionAsync(
                connection, context, "Close", context.Version, context.AdminId);
            Assert.True(first.Applied);
            Assert.False(first.Conflict);
            Assert.NotEqual(context.Version, first.ResultingVersion);

            var stale = await ApplyActionAsync(
                connection, context, "ReturnToBot", context.Version, context.AdminId);
            Assert.False(stale.Applied);
            Assert.True(stale.Found);
            Assert.True(stale.Conflict);

            await using var command = new SqlCommand(
                "SELECT LifecycleStatus, AttentionMode FROM dbo.Conversation WHERE ConversationId = @ConversationId;",
                connection);
            command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt)
                { Value = context.ConversationId });
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Cerrada", reader.GetString(0));
            Assert.Equal("Bot", reader.GetString(1));
        }
        finally
        {
            await using var cleanup = new SqlCommand(
                "DELETE dbo.ConversationAuditEvent WHERE ConversationId = @ConversationId; " +
                "DELETE dbo.Conversation WHERE ConversationId = @ConversationId; " +
                "DELETE dbo.Contact WHERE ContactId = @ContactId;",
                connection);
            cleanup.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt)
                { Value = context.ConversationId });
            cleanup.Parameters.Add(new SqlParameter("@ContactId", SqlDbType.BigInt)
                { Value = context.ContactId });
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task<ConversationContext> CreateConversationAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            """
            SELECT TOP (1) tenant.TenantId, membership.AdminId
            FROM dbo.Tenant AS tenant
            INNER JOIN dbo.TenantMembership AS membership ON membership.TenantId = tenant.TenantId
            WHERE tenant.IsActive = 1 AND membership.IsActive = 1
            ORDER BY membership.TenantMembershipId;
            """,
            connection);
        await using var contextReader = await command.ExecuteReaderAsync();
        Assert.True(await contextReader.ReadAsync(), "The LocalDB fixture requires an active Tenant membership.");
        var tenantId = contextReader.GetGuid(0);
        var adminId = contextReader.GetInt64(1);
        await contextReader.CloseAsync();

        var userId = $"concurrency-{Guid.NewGuid():N}";
        await using var insert = new SqlCommand(
            """
            INSERT dbo.Contact (TenantId, IntegrationId, WhatsAppUserId, DisplayName)
            SELECT @TenantId, integration.IntegrationId, @UserId, N'Concurrency test'
            FROM dbo.WhatsAppIntegration AS integration
            WHERE integration.TenantId = @TenantId;
            DECLARE @ContactId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.Conversation
                (TenantId, IntegrationId, ContactId, BasicStatus, LifecycleStatus, AttentionMode, LastMessageAtUtc)
            SELECT @TenantId, contact.IntegrationId, @ContactId, N'Open', N'Abierta', N'Bot', SYSUTCDATETIME()
            FROM dbo.Contact AS contact
            WHERE contact.ContactId = @ContactId;
            DECLARE @ConversationId bigint = CONVERT(bigint, SCOPE_IDENTITY());
            SELECT @ContactId AS ContactId, @ConversationId AS ConversationId,
                   conversation.Version, @TenantId AS TenantId
            FROM dbo.Conversation AS conversation
            WHERE conversation.ConversationId = @ConversationId;
            """,
            connection);
        insert.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId });
        insert.Parameters.Add(new SqlParameter("@UserId", SqlDbType.NVarChar, 64) { Value = userId });
        await using var reader = await insert.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new ConversationContext(
            tenantId,
            adminId,
            reader.GetInt64(reader.GetOrdinal("ContactId")),
            reader.GetInt64(reader.GetOrdinal("ConversationId")),
            (byte[])reader[reader.GetOrdinal("Version")]);
    }

    private static async Task<ActionResult> ApplyActionAsync(
        SqlConnection connection,
        ConversationContext context,
        string action,
        byte[] expectedVersion,
        long adminId)
    {
        await using var command = new SqlCommand("dbo.sp_Conversation_ApplyAction", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = context.TenantId });
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = context.ConversationId });
        command.Parameters.Add(new SqlParameter("@Action", SqlDbType.NVarChar, 32) { Value = action });
        command.Parameters.Add(new SqlParameter("@ExpectedVersion", SqlDbType.Binary, 8) { Value = expectedVersion });
        command.Parameters.Add(new SqlParameter("@ActorType", SqlDbType.NVarChar, 16) { Value = "Admin" });
        command.Parameters.Add(new SqlParameter("@Origin", SqlDbType.NVarChar, 32) { Value = "AdminPanel" });
        command.Parameters.Add(new SqlParameter("@ActorAdminId", SqlDbType.BigInt) { Value = adminId });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new ActionResult(
            reader.GetBoolean(reader.GetOrdinal("Applied")),
            reader.GetBoolean(reader.GetOrdinal("Found")),
            reader.GetBoolean(reader.GetOrdinal("Conflict")),
            reader.IsDBNull(reader.GetOrdinal("ResultingVersion"))
                ? null
                : (byte[])reader[reader.GetOrdinal("ResultingVersion")]);
    }

    private sealed record ConversationContext(
        Guid TenantId,
        long AdminId,
        long ContactId,
        long ConversationId,
        byte[] Version);

    private sealed record ActionResult(
        bool Applied,
        bool Found,
        bool Conflict,
        byte[]? ResultingVersion);
}
