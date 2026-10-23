using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Entities;

public sealed record Administrator(long AdminId, string Email, string PasswordHash, bool IsActive)
{
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastLoginAtUtc { get; init; }
}

public sealed record WhatsAppIntegration(Guid IntegrationId, string? WabaId, string? PhoneNumberId,
    string? BusinessPhoneNumber, string? DisplayName, ConnectionState ConnectionState,
    DateTimeOffset? ConnectedAtUtc, bool IsActive)
{
    public bool HasCompleteConnection =>
        ConnectionState == ConnectionState.Connected &&
        IsActive &&
        ConnectedAtUtc.HasValue &&
        !string.IsNullOrWhiteSpace(WabaId) &&
        !string.IsNullOrWhiteSpace(PhoneNumberId) &&
        !string.IsNullOrWhiteSpace(BusinessPhoneNumber) &&
        !string.IsNullOrWhiteSpace(AccessTokenSecretRef) &&
        !string.IsNullOrWhiteSpace(MetaAppSecretRef) &&
        !string.IsNullOrWhiteSpace(WebhookVerifyTokenRef);

    public string? LastConnectionErrorCode { get; init; }
    public string? AccessTokenSecretRef { get; init; }
    public string? MetaAppSecretRef { get; init; }
    public string? WebhookVerifyTokenRef { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record BotConfiguration(Guid IntegrationId, bool IsBotEnabled, BotReplyMode ReplyMode)
{
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record FrequentResponse(long FrequentResponseId, Guid IntegrationId, string QuestionOrIntent,
    string AnswerText, int Priority, string? Category, bool IsActive)
{
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset ModifiedAtUtc { get; init; }
}

public sealed record FrequentResponseExpression(long ExpressionId, long FrequentResponseId,
    string ExpressionText, string NormalizedExpression);

public sealed record Contact(long ContactId, Guid IntegrationId, string WhatsAppUserId, string? DisplayName)
{
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record Conversation(long ConversationId, Guid IntegrationId, long ContactId,
    ConversationStatus BasicStatus, DateTimeOffset LastMessageAtUtc)
{
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record Message(long MessageId, Guid IntegrationId, long ConversationId,
    string? ProviderMessageId, MessageDirection Direction, MessageType MessageType,
    string? ContentText, ReplySource? ReplySource, ProcessingState ProcessingState,
    DeliveryState? DeliveryState, DateTimeOffset CreatedAtUtc)
{
    public long? FrequentResponseId { get; init; }
    public DateTimeOffset? ProviderTimestampUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string? FailureCode { get; init; }
}

public sealed record MediaAttachment(long MediaAttachmentId, long MessageId, MediaKind MediaKind,
    string? MimeType, long? SizeBytes, string? StorageReference, MediaAvailabilityState AvailabilityState)
{
    public string? MetaMediaId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed record Transcript(long MessageId, string? TranscriptText, TranscriptState State)
{
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record WebhookInboxEvent(long WebhookInboxEventId, string EventKey, string EventType,
    string NormalizedEventJson, WebhookInboxState State)
{
    public Guid? IntegrationId { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
    public DateTimeOffset? ProcessedAtUtc { get; init; }
    public string? LastFailureCode { get; init; }
}

public sealed record MessageOutbox(long MessageId, Guid IntegrationId, MessageOutboxState State,
    int AttemptCount, DateTimeOffset NextAttemptAtUtc)
{
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
