using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class MediaRepository(StoredProcedureExecutor executor) : IMediaRepository
{
    public async Task<MediaAttachmentRecord> UpsertPendingAsync(
        Guid tenantId,
        long conversationId,
        long messageId,
        string metaMediaId,
        MediaKind mediaKind,
        string? mimeType,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_MediaAttachment_UpsertPending",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new SqlParameter("@MetaMediaId", SqlDbType.NVarChar, 128) { Value = metaMediaId },
                new SqlParameter("@MediaKind", SqlDbType.NVarChar, 16) { Value = mediaKind.ToString() },
                new SqlParameter("@MimeType", SqlDbType.NVarChar, 128)
                    { Value = (object?)mimeType ?? DBNull.Value },
                new SqlParameter("@ExpiresAtUtc", SqlDbType.DateTime2)
                    { Scale = 3, Value = expiresAtUtc.UtcDateTime }
            ],
            MapAttachment,
            cancellationToken)
        ?? throw new InvalidOperationException("The pending media attachment was not returned.");

    public Task SetAvailableAsync(
        Guid tenantId,
        long conversationId,
        long messageId,
        string storageReference,
        string mimeType,
        long sizeBytes,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_MediaAttachment_SetAvailable",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new SqlParameter("@StorageReference", SqlDbType.NVarChar, 512) { Value = storageReference },
                new SqlParameter("@MimeType", SqlDbType.NVarChar, 128) { Value = mimeType },
                new SqlParameter("@SizeBytes", SqlDbType.BigInt) { Value = sizeBytes },
                new SqlParameter("@ExpiresAtUtc", SqlDbType.DateTime2)
                    { Scale = 3, Value = expiresAtUtc.UtcDateTime }
            ],
            cancellationToken);

    public Task SetFailedAsync(
        Guid tenantId,
        long conversationId,
        long messageId,
        string? storageReference,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_MediaAttachment_SetFailed",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new SqlParameter("@StorageReference", SqlDbType.NVarChar, 512)
                    { Value = (object?)storageReference ?? DBNull.Value }
            ],
            cancellationToken);

    public async Task<AuthorizedMediaRecord?> GetForAccessAsync(
        Guid tenantId, long messageId, CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_MediaAttachment_GetForAccess",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId }
            ],
            reader => new AuthorizedMediaRecord(
                reader.GetGuid(reader.GetOrdinal("TenantId")),
                reader.GetInt64(reader.GetOrdinal("ConversationId")),
                reader.GetInt64(reader.GetOrdinal("MessageId")),
                Enum.Parse<MediaKind>(reader.GetString(reader.GetOrdinal("MediaKind")), false),
                reader.IsDBNull(reader.GetOrdinal("MimeType"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("MimeType")),
                reader.IsDBNull(reader.GetOrdinal("SizeBytes"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("SizeBytes")),
                reader.IsDBNull(reader.GetOrdinal("StorageReference"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("StorageReference")),
                Enum.Parse<MediaAvailabilityState>(
                    reader.GetString(reader.GetOrdinal("AvailabilityState")), false),
                reader.IsDBNull(reader.GetOrdinal("ExpiresAtUtc"))
                    ? null
                    : ToUtc(reader, "ExpiresAtUtc")),
            cancellationToken);

    public async Task<IReadOnlyList<MediaAttachmentRecord>> ListExpiredAsync(
        DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken) =>
        (await executor.QueryAsync(
            "dbo.sp_MediaAttachment_ListExpired",
            [
                new SqlParameter("@NowUtc", SqlDbType.DateTime2)
                    { Scale = 3, Value = nowUtc.UtcDateTime },
                new SqlParameter("@BatchSize", SqlDbType.Int) { Value = batchSize }
            ],
            MapAttachment,
            cancellationToken)).ToArray();

    public Task MarkDeletedAsync(
        Guid tenantId, long messageId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_MediaAttachment_MarkDeleted",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId }
            ],
            cancellationToken);

    public Task UpsertTranscriptPendingAsync(
        Guid tenantId, long conversationId, long messageId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_Transcript_UpsertPending",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId }
            ],
            cancellationToken);

    public Task SetTranscriptAsync(
        Guid tenantId,
        long conversationId,
        long messageId,
        string? transcriptText,
        TranscriptState state,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_Transcript_SetResult",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new SqlParameter("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new SqlParameter("@TranscriptText", SqlDbType.NVarChar, -1)
                    { Value = (object?)transcriptText ?? DBNull.Value },
                new SqlParameter("@State", SqlDbType.NVarChar, 16) { Value = state.ToString() }
            ],
            cancellationToken);

    private static MediaAttachmentRecord MapAttachment(SqlDataReader reader) => new(
        reader.GetInt64(reader.GetOrdinal("MediaAttachmentId")),
        reader.GetGuid(reader.GetOrdinal("TenantId")),
        reader.GetInt64(reader.GetOrdinal("ConversationId")),
        reader.GetInt64(reader.GetOrdinal("MessageId")),
        reader.IsDBNull(reader.GetOrdinal("MetaMediaId"))
            ? null
            : reader.GetString(reader.GetOrdinal("MetaMediaId")),
        Enum.Parse<MediaKind>(reader.GetString(reader.GetOrdinal("MediaKind")), false),
        reader.IsDBNull(reader.GetOrdinal("MimeType"))
            ? null
            : reader.GetString(reader.GetOrdinal("MimeType")),
        reader.IsDBNull(reader.GetOrdinal("SizeBytes"))
            ? null
            : reader.GetInt64(reader.GetOrdinal("SizeBytes")),
        reader.IsDBNull(reader.GetOrdinal("StorageReference"))
            ? null
            : reader.GetString(reader.GetOrdinal("StorageReference")),
        Enum.Parse<MediaAvailabilityState>(
            reader.GetString(reader.GetOrdinal("AvailabilityState")), false),
        ToUtc(reader, "CreatedAtUtc"),
        reader.IsDBNull(reader.GetOrdinal("ExpiresAtUtc"))
            ? null
            : ToUtc(reader, "ExpiresAtUtc"));

    private static DateTimeOffset ToUtc(SqlDataReader reader, string columnName) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(columnName)), DateTimeKind.Utc));
}
