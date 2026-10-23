using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class MessageRepository(StoredProcedureExecutor executor) : IMessageRepository
{
    private sealed record DetailRow(
        ConversationSummaryRecord Summary,
        ConversationMessageRecord? Message);

    public async Task<ConversationDetailRecord?> GetHistoryAsync(
        Guid integrationId,
        long conversationId,
        long? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync(
            "dbo.sp_Conversation_GetDetail",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@BeforeMessageId", SqlDbType.BigInt)
                    { Value = (object?)beforeMessageId ?? DBNull.Value },
                new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize }
            ],
            MapDetailRow,
            cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        return new ConversationDetailRecord(
            rows[0].Summary,
            rows.Where(row => row.Message is not null).Select(row => row.Message!).ToArray());
    }

    public async Task<InboundMessageInsertResult> InsertInboundAsync(
        Guid integrationId,
        long conversationId,
        NormalizedMetaEvent inboundEvent,
        CancellationToken cancellationToken)
    {
        var result = await executor.QuerySingleAsync(
            "dbo.sp_Message_InsertInbound",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@ProviderMessageId", SqlDbType.NVarChar, 256)
                    { Value = inboundEvent.ProviderMessageId! },
                new SqlParameter("@MessageType", SqlDbType.NVarChar, 24) { Value = MessageType.Text.ToString() },
                new SqlParameter("@ContentText", SqlDbType.NVarChar, -1) { Value = inboundEvent.Text! },
                new SqlParameter("@ProviderTimestampUtc", SqlDbType.DateTime2)
                    { Scale = 3, Value = inboundEvent.ProviderTimestampUtc!.Value.UtcDateTime }
            ],
            reader => new InboundMessageInsertResult(
                reader.GetBoolean(reader.GetOrdinal("Inserted")),
                reader.GetInt64(reader.GetOrdinal("MessageId"))),
            cancellationToken);
        return result ?? throw new InvalidOperationException("The inbound WhatsApp message could not be persisted.");
    }

    public Task SetProcessingOutcomeAsync(
        long messageId,
        ProcessingState state,
        string? safeOutcomeCode,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_Message_SetProcessingOutcome",
            [
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new SqlParameter("@ProcessingState", SqlDbType.NVarChar, 24) { Value = state.ToString() },
                new SqlParameter("@SafeOutcomeCode", SqlDbType.NVarChar, 100)
                    { Value = (object?)safeOutcomeCode ?? DBNull.Value }
            ],
            cancellationToken);

    public async Task<IReadOnlyList<OutboundMessageWorkItem>> ClaimOutboxBatchAsync(
        int batchSize, CancellationToken cancellationToken) =>
        (await executor.QueryAsync(
            "dbo.sp_MessageOutbox_ClaimBatch",
            [new SqlParameter("@BatchSize", SqlDbType.Int) { Value = batchSize }],
            reader => new OutboundMessageWorkItem(
                reader.GetInt64(reader.GetOrdinal("MessageId")),
                reader.GetGuid(reader.GetOrdinal("IntegrationId")),
                reader.GetString(reader.GetOrdinal("PhoneNumberId")),
                reader.GetString(reader.GetOrdinal("Recipient")),
                reader.GetString(reader.GetOrdinal("ContentText")),
                new DateTimeOffset(DateTime.SpecifyKind(
                    reader.GetDateTime(reader.GetOrdinal("LastCustomerMessageAtUtc")), DateTimeKind.Utc)),
                reader.GetInt32(reader.GetOrdinal("AttemptCount"))),
            cancellationToken)).ToArray();

    public Task SetOutboxResultAsync(
        long messageId,
        MetaMessageSendResult result,
        bool isTerminal,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_Message_UpdateOutboundProviderResult",
            [
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new SqlParameter("@Succeeded", SqlDbType.Bit) { Value = result.Succeeded },
                new SqlParameter("@ProviderMessageId", SqlDbType.NVarChar, 256)
                    { Value = (object?)result.ProviderMessageId ?? DBNull.Value },
                new SqlParameter("@SafeFailureCode", SqlDbType.NVarChar, 100)
                    { Value = (object?)result.SafeFailureCode ?? DBNull.Value },
                new SqlParameter("@IsTerminal", SqlDbType.Bit) { Value = isTerminal }
            ],
            cancellationToken);

    public Task SetOutboxStateAsync(
        long messageId,
        MessageOutboxState state,
        int retryDelaySeconds,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_MessageOutbox_SetState",
            [
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new SqlParameter("@State", SqlDbType.NVarChar, 16) { Value = state.ToString() },
                new SqlParameter("@RetryDelaySeconds", SqlDbType.Int) { Value = retryDelaySeconds }
            ],
            cancellationToken);

    private static DetailRow MapDetailRow(SqlDataReader reader)
    {
        var summary = new ConversationSummaryRecord(
            reader.GetInt64(reader.GetOrdinal("ConversationId")),
            reader.GetString(reader.GetOrdinal("Contact")),
            ToUtc(reader, "LastMessageAtUtc"),
            Enum.Parse<ConversationStatus>(reader.GetString(reader.GetOrdinal("Status")), false),
            reader.IsDBNull(reader.GetOrdinal("LastMessagePreview"))
                ? null
                : reader.GetString(reader.GetOrdinal("LastMessagePreview")));

        var messageIdOrdinal = reader.GetOrdinal("MessageId");
        if (reader.IsDBNull(messageIdOrdinal))
        {
            return new DetailRow(summary, null);
        }

        var replySourceOrdinal = reader.GetOrdinal("ReplySource");
        var deliveryStateOrdinal = reader.GetOrdinal("DeliveryState");
        var message = new ConversationMessageRecord(
            reader.GetInt64(messageIdOrdinal),
            Enum.Parse<MessageDirection>(reader.GetString(reader.GetOrdinal("Direction")), false),
            Enum.Parse<MessageType>(reader.GetString(reader.GetOrdinal("MessageType")), false),
            reader.IsDBNull(reader.GetOrdinal("ContentText"))
                ? null
                : reader.GetString(reader.GetOrdinal("ContentText")),
            reader.IsDBNull(reader.GetOrdinal("Transcription"))
                ? null
                : reader.GetString(reader.GetOrdinal("Transcription")),
            reader.GetBoolean(reader.GetOrdinal("MediaAvailable")),
            reader.IsDBNull(replySourceOrdinal)
                ? null
                : Enum.Parse<ReplySource>(reader.GetString(replySourceOrdinal), false),
            Enum.Parse<ProcessingState>(reader.GetString(reader.GetOrdinal("ProcessingState")), false),
            reader.IsDBNull(deliveryStateOrdinal)
                ? null
                : Enum.Parse<DeliveryState>(reader.GetString(deliveryStateOrdinal), false),
            reader.IsDBNull(reader.GetOrdinal("OutcomeCode"))
                ? null
                : reader.GetString(reader.GetOrdinal("OutcomeCode")),
            ToUtc(reader, "CreatedAtUtc"));
        return new DetailRow(summary, message);
    }

    private static DateTimeOffset ToUtc(SqlDataReader reader, string columnName) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(columnName)), DateTimeKind.Utc));
}
