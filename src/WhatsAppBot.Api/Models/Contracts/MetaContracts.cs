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
    string PhoneNumberId,
    string Recipient,
    string Text,
    DateTimeOffset LastCustomerMessageAtUtc,
    int AttemptCount);

public static class MessageOutcomeCodes
{
    public const string MessagingWindowClosed = "MessagingWindowClosed";
    public const string TemplateRequired = "TemplateRequired";
    public const string BotDisabled = "BotDisabled";
    public const string NoFrequentResponse = "NoFrequentResponse";
    public const string AiFallbackNotConfigured = "AiFallbackNotConfigured";
    public const string UnsupportedMessageType = "UnsupportedMessageType";
    public const string MetaSendFailed = "MetaSendFailed";
}

public static class MetaWebhookEventTypes
{
    public const string InboundText = "InboundText";
    public const string InboundAudio = "InboundAudio";
    public const string InboundImage = "InboundImage";
    public const string DeliveryStatus = "DeliveryStatus";
    public const string UnsupportedMessage = "UnsupportedMessage";
}
