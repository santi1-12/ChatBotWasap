using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class ConversationRepository(StoredProcedureExecutor executor) : IConversationRepository
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
        return new ListRow(summary, reader.GetInt64(reader.GetOrdinal("TotalCount")));
    }

    private static DateTimeOffset ToUtc(SqlDataReader reader, string columnName) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(columnName)), DateTimeKind.Utc));
}
