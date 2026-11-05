using WhatsAppBot.Shared.Enums;
using WhatsAppBot.Api.Models.Entities;

namespace WhatsAppBot.Api.Models.Contracts;

public sealed record MediaStorageObject(string StorageReference, long SizeBytes);

public sealed record MetaMediaDownload(
    string MimeType,
    long? DeclaredSizeBytes,
    byte[] Content);

public sealed record NormalizedAudio(byte[] Content, string ContentType);

public sealed record InboundMediaContext(
    WhatsAppIntegration Integration,
    BotConfiguration Configuration,
    Guid TenantId,
    long ConversationId,
    long MessageId,
    DateTimeOffset ProviderTimestampUtc,
    MediaKind MediaKind,
    string MimeType,
    byte[] Content);

public sealed record MediaAttachmentRecord(
    long MediaAttachmentId,
    Guid TenantId,
    long ConversationId,
    long MessageId,
    string? MetaMediaId,
    MediaKind MediaKind,
    string? MimeType,
    long? SizeBytes,
    string? StorageReference,
    MediaAvailabilityState AvailabilityState,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record AuthorizedMediaRecord(
    Guid TenantId,
    long ConversationId,
    long MessageId,
    MediaKind MediaKind,
    string? MimeType,
    long? SizeBytes,
    string? StorageReference,
    MediaAvailabilityState AvailabilityState,
    DateTimeOffset? ExpiresAtUtc)
{
    public bool IsExpired(DateTimeOffset nowUtc) =>
        ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= nowUtc;
}

public enum MediaAccessStatus
{
    Authorized,
    NotFound,
    Expired,
    Unavailable
}

public sealed record MediaAccessResult(
    MediaAccessStatus Status,
    Stream? Content,
    string? MimeType,
    long? SizeBytes);

public class MediaProcessingException(string safeCode, bool isTransient = false)
    : Exception(safeCode)
{
    public string SafeCode { get; } = safeCode;
    public bool IsTransient { get; } = isTransient;
}

public sealed class MediaCapabilityUnavailableException(string safeCode)
    : MediaProcessingException(safeCode);
