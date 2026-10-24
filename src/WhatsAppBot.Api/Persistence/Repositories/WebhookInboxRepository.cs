using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class WebhookInboxRepository(StoredProcedureExecutor executor) : IWebhookInboxRepository
{
    public async Task<bool> InsertAsync(
        NormalizedMetaEvent webhookEvent, CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_WebhookInbox_Insert",
            [
                new SqlParameter("@EventKey", SqlDbType.NVarChar, 256) { Value = webhookEvent.EventKey },
                new SqlParameter("@EventType", SqlDbType.NVarChar, 80) { Value = webhookEvent.EventType },
                new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64) { Value = webhookEvent.PhoneNumberId },
                new SqlParameter("@WabaId", SqlDbType.NVarChar, 64)
                    { Value = (object?)webhookEvent.WabaId ?? DBNull.Value },
                new SqlParameter("@NormalizedEventJson", SqlDbType.NVarChar, -1)
                    { Value = JsonSerializer.Serialize(webhookEvent) }
            ],
            reader => reader.GetBoolean(reader.GetOrdinal("Inserted")),
            cancellationToken);

    public async Task<IReadOnlyList<NormalizedMetaEvent>> ClaimBatchAsync(
        int batchSize, CancellationToken cancellationToken) =>
        (await executor.QueryAsync(
            "dbo.sp_WebhookInbox_ClaimBatch",
            [new SqlParameter("@BatchSize", SqlDbType.Int) { Value = batchSize }],
            reader => JsonSerializer.Deserialize<NormalizedMetaEvent>(
                          reader.GetString(reader.GetOrdinal("NormalizedEventJson")))
                      ?? throw new InvalidDataException("A normalized webhook event could not be read."),
            cancellationToken)).ToArray();

    public Task SetStateAsync(
        string eventKey,
        WebhookInboxState state,
        string? safeFailureCode,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_WebhookInbox_SetState",
            [
                new SqlParameter("@EventKey", SqlDbType.NVarChar, 256) { Value = eventKey },
                new SqlParameter("@State", SqlDbType.NVarChar, 16) { Value = state.ToString() },
                new SqlParameter("@SafeFailureCode", SqlDbType.NVarChar, 100)
                    { Value = (object?)safeFailureCode ?? DBNull.Value }
            ],
            cancellationToken);
}
