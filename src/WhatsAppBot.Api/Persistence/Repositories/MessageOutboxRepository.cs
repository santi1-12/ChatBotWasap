using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class MessageOutboxRepository(StoredProcedureExecutor executor) : IMessageOutboxRepository
{
    public async Task<OutboundMessageInsertResult> CreatePendingAsync(
        long inboundMessageId,
        Guid integrationId,
        long conversationId,
        string text,
        ReplySource source,
        long? frequentResponseId,
        CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_Message_CreateOutboundPending",
            [
                new SqlParameter("@InboundMessageId", SqlDbType.BigInt) { Value = inboundMessageId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@ContentText", SqlDbType.NVarChar, -1) { Value = text },
                new SqlParameter("@ReplySource", SqlDbType.NVarChar, 32) { Value = source.ToString() },
                new SqlParameter("@FrequentResponseId", SqlDbType.BigInt)
                    { Value = (object?)frequentResponseId ?? DBNull.Value }
            ],
            reader => new OutboundMessageInsertResult(
                reader.GetBoolean(reader.GetOrdinal("Inserted")),
                reader.IsDBNull(reader.GetOrdinal("MessageId"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("MessageId")),
                reader.IsDBNull(reader.GetOrdinal("OutcomeCode"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("OutcomeCode"))),
            cancellationToken)
        ?? throw new InvalidOperationException("The outbound message and outbox could not be persisted.");

    public async Task<ManualMessageInsertResult> CreateAdminPendingAsync(
        Guid tenantId,
        long conversationId,
        long adminId,
        string text,
        string expectedVersion,
        CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_Message_CreateAdminOutboundPending",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@AdminId", SqlDbType.BigInt) { Value = adminId },
                new SqlParameter("@ContentText", SqlDbType.NVarChar, -1) { Value = text },
                new SqlParameter("@ExpectedVersion", SqlDbType.Binary, 8)
                    { Value = ConversationVersion.Parse(expectedVersion) }
            ],
            reader => new ManualMessageInsertResult(
                reader.GetBoolean(reader.GetOrdinal("Inserted")),
                reader.GetBoolean(reader.GetOrdinal("Found")),
                reader.GetBoolean(reader.GetOrdinal("Conflict")),
                reader.IsDBNull(reader.GetOrdinal("MessageId"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("MessageId")),
                reader.IsDBNull(reader.GetOrdinal("OutcomeCode"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("OutcomeCode")),
                reader.IsDBNull(reader.GetOrdinal("CreatedAtUtc"))
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind(
                        reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")), DateTimeKind.Utc))),
            cancellationToken)
        ?? throw new InvalidOperationException("The manual outbound message did not return a result.");
}
