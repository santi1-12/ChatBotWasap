using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Contracts;

public sealed record NormalizedMetaEvent(
    string EventKey,
    string EventType,
    string PhoneNumberId,
    string? WabaId,
    string? WhatsAppUserId,
    string? ContactDisplayName,
    string? ProviderMessageId,
    string? Text,
    DateTimeOffset? ProviderTimestampUtc,
    string? DeliveryStatus)
{
    public MessageType? MessageType { get; init; }
    public string? MediaId { get; init; }
    public string? MediaMimeType { get; init; }
    public string? MediaCaption { get; init; }
}

public sealed record MetaSignupResult(
    Guid IntegrationId,
    string WabaId,
    string PhoneNumberId,
    string BusinessPhoneNumber,
    string? DisplayName,
    string AccessTokenSecretRef,
    string MetaAppSecretRef,
    string WebhookVerifyTokenRef);

public sealed record MetaPhoneNumberSummary(string PhoneNumberId);

public sealed record MetaPhoneNumberDetails(string DisplayPhoneNumber, string? VerifiedName);

public sealed record MetaMessageSendResult(
    bool Succeeded,
    string? ProviderMessageId,
    string? SafeFailureCode,
    bool IsTransient);

public sealed record MessagingPolicyDecision(bool IsAllowed, string? OutcomeCode);

public sealed record InboundMessageInsertResult(bool Inserted, long MessageId);

public sealed record OutboundMessageInsertResult(
    bool Inserted,
    long? MessageId,
    string? OutcomeCode);

public sealed record OutboundMessageWorkItem(
    long MessageId,
    Guid IntegrationId,
    Guid TenantId,
    long ConversationId,
    string PhoneNumberId,
    string Recipient,
    string Text,
    string ConversationVersion,
    DateTimeOffset LastCustomerMessageAtUtc,
    int AttemptCount);

public sealed record OutboxRevalidationResult(bool IsAllowed, string? OutcomeCode);

public sealed record ManualMessageInsertResult(
    bool Inserted,
    bool Found,
    bool Conflict,
    long? MessageId,
    string? OutcomeCode,
    DateTimeOffset? CreatedAtUtc);

public sealed record DeliveryStatusApplyResult(bool Found, bool Applied, string? DeliveryState);

public static class MessageOutcomeCodes
{
    public const string MessagingWindowClosed = "MessagingWindowClosed";
    public const string TemplateRequired = "TemplateRequired";
    public const string BotDisabled = "BotDisabled";
    public const string NoFrequentResponse = "NoFrequentResponse";
    public const string AiFallbackNotConfigured = "AiFallbackNotConfigured";
    public const string UnsupportedMessageType = "UnsupportedMessageType";
    public const string MetaSendFailed = "MetaSendFailed";
    public const string ConversationInHumanMode = "ConversationInHumanMode";
    public const string ConversationClosed = "ConversationClosed";
    public const string ConversationVersionConflict = "ConversationVersionConflict";
    public const string IntegrationUnavailable = "IntegrationUnavailable";
    public const string ManualSendFailed = "ManualSendFailed";
    public const string BlockingError = "BlockingError";
    public const string MetaDeliveryFailed = "MetaDeliveryFailed";
}

public static class MetaWebhookEventTypes
{
    public const string InboundText = "InboundText";
    public const string InboundAudio = "InboundAudio";
    public const string InboundImage = "InboundImage";
    public const string DeliveryStatus = "DeliveryStatus";
    public const string UnsupportedMessage = "UnsupportedMessage";
}
