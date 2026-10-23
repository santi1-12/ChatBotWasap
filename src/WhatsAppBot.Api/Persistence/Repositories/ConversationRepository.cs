using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Requests;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class ConversationRepository(StoredProcedureExecutor executor)
    : IConversationRepository, IConversationActionRepository, IConversationStateRepository
{
    private sealed record ListRow(ConversationSummaryRecord? Record, long TotalCount);

    public async Task<long> GetOrCreateAsync(
        Guid integrationId,
        Guid tenantId,
        long contactId,
        DateTimeOffset lastMessageAtUtc,
        bool isBotEnabled,
        CancellationToken cancellationToken)
    {
        var conversationId = await executor.QuerySingleAsync(
            "dbo.sp_Conversation_GetOrCreate",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ContactId", SqlDbType.BigInt) { Value = contactId },
                new SqlParameter("@LastMessageAtUtc", SqlDbType.DateTime2)
                    { Scale = 3, Value = lastMessageAtUtc.UtcDateTime },
                new SqlParameter("@IsBotEnabled", SqlDbType.Bit) { Value = isBotEnabled }
            ],
            reader => reader.GetInt64(reader.GetOrdinal("ConversationId")),
            cancellationToken);
        return conversationId > 0
            ? conversationId
            : throw new InvalidOperationException("The WhatsApp conversation could not be persisted.");
    }

    public async Task<ConversationPage> ListAsync(
        Guid integrationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync(
            "dbo.sp_Conversation_List",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@Page", SqlDbType.Int) { Value = page },
                new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize }
            ],
            MapListRow,
            cancellationToken);

        return new ConversationPage(
            rows.Where(row => row.Record is not null).Select(row => row.Record!).ToArray(),
            page,
            pageSize,
            rows.Count == 0 ? 0 : checked((int)rows[0].TotalCount));
    }

    public async Task<ConversationActionApplyResult> ApplyAsync(
        Guid tenantId,
        long conversationId,
        ConversationActionCommand command,
        long? actorAdminId,
        CancellationToken cancellationToken)
    {
        var result = await executor.QuerySingleAsync(
            "dbo.sp_Conversation_ApplyAction",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@Action", SqlDbType.NVarChar, 32) { Value = command.Action.ToString() },
                new SqlParameter("@ExpectedVersion", SqlDbType.Binary, 8)
                    { Value = ConversationVersion.Parse(command.ExpectedVersion) },
                new SqlParameter("@ActorType", SqlDbType.NVarChar, 16) { Value = command.ActorType.ToString() },
                new SqlParameter("@Origin", SqlDbType.NVarChar, 32) { Value = command.Origin.ToString() },
                new SqlParameter("@ActorAdminId", SqlDbType.BigInt)
                    { Value = (object?)actorAdminId ?? DBNull.Value },
                new SqlParameter("@ReasonCode", SqlDbType.NVarChar, 100)
                    { Value = (object?)command.ReasonCode ?? DBNull.Value }
            ],
            MapActionResult,
            cancellationToken);
        return result ?? throw new InvalidOperationException("The conversation action did not return a result.");
    }

    public async Task<bool> SetAssignmentAsync(
        Guid tenantId,
        long conversationId,
        long? assignedAdminId,
        string expectedVersion,
        long actorAdminId,
        CancellationToken cancellationToken)
    {
        var result = await executor.QuerySingleAsync(
            "dbo.sp_Conversation_SetAssignment",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@AssignedAdminId", SqlDbType.BigInt)
                    { Value = (object?)assignedAdminId ?? DBNull.Value },
                new SqlParameter("@ExpectedVersion", SqlDbType.Binary, 8)
                    { Value = ConversationVersion.Parse(expectedVersion) },
                new SqlParameter("@ActorAdminId", SqlDbType.BigInt) { Value = actorAdminId },
                new SqlParameter("@Origin", SqlDbType.NVarChar, 32) { Value = Origin.AdminPanel.ToString() }
            ],
            MapActionResult,
            cancellationToken);
        return result?.Applied == true;
    }

    public async Task<ConversationStateRecord?> GetStateAsync(
        Guid tenantId, long conversationId, CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_Conversation_GetState",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId }
            ],
            reader => new ConversationStateRecord(
                reader.GetGuid(reader.GetOrdinal("TenantId")),
                reader.GetInt64(reader.GetOrdinal("ConversationId")),
                Enum.Parse<AttentionMode>(reader.GetString(reader.GetOrdinal("AttentionMode")), false),
                Enum.Parse<LifecycleStatus>(reader.GetString(reader.GetOrdinal("LifecycleStatus")), false),
                reader.IsDBNull(reader.GetOrdinal("AssignedAdminId"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("AssignedAdminId")),
                ConversationVersion.Format((byte[])reader[reader.GetOrdinal("Version")])),
            cancellationToken);

    public async Task<IReadOnlyList<ConversationAuditEventRecord>> ListAuditAsync(
        Guid tenantId, long conversationId, CancellationToken cancellationToken) =>
        (await executor.QueryAsync(
            "dbo.sp_Conversation_GetAudit",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId }
            ],
            reader => new ConversationAuditEventRecord(
                reader.GetString(reader.GetOrdinal("EventType")),
                Enum.Parse<ActorType>(reader.GetString(reader.GetOrdinal("ActorType")), false),
                Enum.Parse<Origin>(reader.GetString(reader.GetOrdinal("Origin")), false),
                reader.IsDBNull(reader.GetOrdinal("ActorAdminId"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("ActorAdminId")),
                reader.IsDBNull(reader.GetOrdinal("ReasonCode"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("ReasonCode")),
                ToUtc(reader, "OccurredAtUtc"),
                reader.IsDBNull(reader.GetOrdinal("ResultingVersion"))
                    ? null
                    : ConversationVersion.Format((byte[])reader[reader.GetOrdinal("ResultingVersion")])),
            cancellationToken)).ToArray();

    public Task RecordAuditAsync(
        Guid tenantId,
        long conversationId,
        ConversationAuditEventCommand command,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_Conversation_RecordAuditEvent",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@EventType", SqlDbType.NVarChar, 32) { Value = command.EventType },
                new SqlParameter("@PreviousAttentionMode", SqlDbType.NVarChar, 16)
                    { Value = (object?)command.PreviousAttentionMode?.ToString() ?? DBNull.Value },
                new SqlParameter("@NewAttentionMode", SqlDbType.NVarChar, 16)
                    { Value = (object?)command.NewAttentionMode?.ToString() ?? DBNull.Value },
                new SqlParameter("@PreviousLifecycleStatus", SqlDbType.NVarChar, 16)
                    { Value = (object?)command.PreviousLifecycleStatus?.ToString() ?? DBNull.Value },
                new SqlParameter("@NewLifecycleStatus", SqlDbType.NVarChar, 16)
                    { Value = (object?)command.NewLifecycleStatus?.ToString() ?? DBNull.Value },
                new SqlParameter("@ActorType", SqlDbType.NVarChar, 16) { Value = command.ActorType.ToString() },
                new SqlParameter("@Origin", SqlDbType.NVarChar, 32) { Value = command.Origin.ToString() },
                new SqlParameter("@ActorAdminId", SqlDbType.BigInt)
                    { Value = (object?)command.ActorAdminId ?? DBNull.Value },
                new SqlParameter("@ReasonCode", SqlDbType.NVarChar, 100)
                    { Value = (object?)command.ReasonCode ?? DBNull.Value },
                new SqlParameter("@ExpectedVersion", SqlDbType.Binary, 8)
                    { Value = (object?)ConversationVersion.TryParse(command.ExpectedVersion) ?? DBNull.Value },
                new SqlParameter("@ResultingVersion", SqlDbType.Binary, 8)
                    { Value = (object?)ConversationVersion.TryParse(command.ResultingVersion) ?? DBNull.Value }
            ],
            cancellationToken);

    private static ListRow MapListRow(SqlDataReader reader)
    {
        var conversationIdOrdinal = reader.GetOrdinal("ConversationId");
        if (reader.IsDBNull(conversationIdOrdinal))
        {
            return new ListRow(null, reader.GetInt64(reader.GetOrdinal("TotalCount")));
        }

        var summary = new ConversationSummaryRecord(
            reader.GetInt64(conversationIdOrdinal),
            reader.GetString(reader.GetOrdinal("Contact")),
            ToUtc(reader, "LastMessageAtUtc"),
            Enum.Parse<ConversationStatus>(reader.GetString(reader.GetOrdinal("Status")), false),
            reader.IsDBNull(reader.GetOrdinal("LastMessagePreview"))
                ? null
                : reader.GetString(reader.GetOrdinal("LastMessagePreview")));
        summary = summary with
        {
            TenantId = reader.GetGuid(reader.GetOrdinal("TenantId")),
            AttentionMode = Enum.Parse<AttentionMode>(reader.GetString(reader.GetOrdinal("AttentionMode")), false),
            LifecycleStatus = Enum.Parse<LifecycleStatus>(reader.GetString(reader.GetOrdinal("LifecycleStatus")), false),
            AssignedAdminId = reader.IsDBNull(reader.GetOrdinal("AssignedAdminId"))
                ? null
                : reader.GetInt64(reader.GetOrdinal("AssignedAdminId")),
            Version = ConversationVersion.Format((byte[])reader[reader.GetOrdinal("Version")])
        };
        return new ListRow(summary, reader.GetInt64(reader.GetOrdinal("TotalCount")));
    }

    private static ConversationActionApplyResult MapActionResult(SqlDataReader reader) =>
        new(
            reader.GetBoolean(reader.GetOrdinal("Applied")),
            reader.GetBoolean(reader.GetOrdinal("Found")),
            reader.GetBoolean(reader.GetOrdinal("Conflict")),
            reader.IsDBNull(reader.GetOrdinal("ResultingVersion"))
                ? null
                : ConversationVersion.Format((byte[])reader[reader.GetOrdinal("ResultingVersion")]));

    private static DateTimeOffset ToUtc(SqlDataReader reader, string columnName) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(columnName)), DateTimeKind.Utc));
}

internal static class ConversationVersion
{
    public static byte[] Parse(string value)
    {
        var parsed = TryParse(value)
            ?? throw new BusinessValidationException("ExpectedVersion must be an opaque 8-byte conversation version.");
        return parsed;
    }

    public static byte[]? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var base64 = Convert.FromBase64String(value);
            return base64.Length == 8 ? base64 : null;
        }
        catch (FormatException)
        {
            try
            {
                var hex = Convert.FromHexString(value);
                return hex.Length == 8 ? hex : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    public static string Format(byte[] value) => Convert.ToBase64String(value);
}
