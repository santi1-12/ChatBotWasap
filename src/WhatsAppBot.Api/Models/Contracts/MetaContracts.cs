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
    public Guid? IntegrationId { get; init; }
    public Guid? TenantId { get; init; }
    public MessageType? MessageType { get; init; }
    public string? MediaId { get; init; }
    public string? MediaMimeType { get; init; }
    public string? MediaCaption { get; init; }
    public string SourceField { get; init; } = "messages";
    public MessageDirection Direction { get; init; } = MessageDirection.Inbound;
    public ActorType ActorType { get; init; } = ActorType.Customer;
    public Origin Origin { get; init; } = Origin.MetaCloudApi;
    public bool IsHistorical { get; init; }
    public bool IsGroup { get; init; }
    public string? HistoryImportKey { get; init; }
    public int HistoryTotalCount { get; init; }
    public string? RecipientWhatsAppUserId { get; init; }
    public string? SourceEventKey { get; init; }
}

public sealed record MetaSignupResult(
    Guid IntegrationId,
    string WabaId,
    string PhoneNumberId,
    string BusinessPhoneNumber,
    string? DisplayName,
    string AccessTokenSecretRef,
    string MetaAppSecretRef,
    string WebhookVerifyTokenRef)
{
    public Guid TenantId { get; init; }
    public WhatsAppOnboardingType OnboardingType { get; init; }
    public string EffectiveEmbeddedSignupConfigId { get; init; } = string.Empty;
    public bool ImportHistory { get; init; }
}

public sealed record MetaPhoneNumberSummary(string PhoneNumberId);

public sealed record MetaPhoneNumberDetails(string DisplayPhoneNumber, string? VerifiedName);

public sealed record MetaMediaMetadata(string Url, string MimeType, long? FileSizeBytes);

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
    public const string AiConfigurationMissing = "AiConfigurationMissing";
    public const string AiProviderTimeout = "AiProviderTimeout";
    public const string AiProviderUnavailable = "AiProviderUnavailable";
    public const string AiInvalidResponse = "AiInvalidResponse";
    public const string AiUnableToResolve = "AiUnableToResolve";
    public const string UnsupportedMessageType = "UnsupportedMessageType";
    public const string MetaSendFailed = "MetaSendFailed";
    public const string MetaRequestTimedOut = "MetaRequestTimedOut";
    public const string MetaUnavailable = "MetaUnavailable";
    public const string MetaCredentialsMissing = "MetaCredentialsMissing";
    public const string MetaConfigurationMissing = "MetaConfigurationMissing";
    public const string MetaInvalidResponse = "MetaInvalidResponse";
    public const string MetaSecretStoreUnavailable = "MetaSecretStoreUnavailable";
    public const string MetaRateLimited = "MetaRateLimited";
    public const string MetaAuthorizationFailed = "MetaAuthorizationFailed";
    public const string MetaRequestRejected = "MetaRequestRejected";
    public const string MetaRequestFailed = "MetaRequestFailed";
    public const string MetaEmptyResponse = "MetaEmptyResponse";
    public const string ConversationInHumanMode = "ConversationInHumanMode";
    public const string ConversationClosed = "ConversationClosed";
    public const string ConversationVersionConflict = "ConversationVersionConflict";
    public const string IntegrationUnavailable = "IntegrationUnavailable";
    public const string IntegrationMigrationInProgress = "IntegrationMigrationInProgress";
    public const string ManualSendFailed = "ManualSendFailed";
    public const string ManualReplyRequiresHuman = "ManualReplyRequiresHuman";
    public const string ManualReplyNotAuthorized = "ManualReplyNotAuthorized";
    public const string BlockingError = "BlockingError";
    public const string MetaDeliveryFailed = "MetaDeliveryFailed";
    public const string MediaExpired = "MediaExpired";
    public const string HandoffToHuman = "HandoffToHuman";
    public const string MediaDownloadFailed = "MediaDownloadFailed";
    public const string MediaStorageFailed = "MediaStorageFailed";
    public const string MediaInvalidMime = "MediaInvalidMime";
    public const string MediaTooLarge = "MediaTooLarge";
    public const string TranscriptionFailed = "TranscriptionFailed";
    public const string ImageAnalysisUnavailable = "ImageAnalysisUnavailable";
    public const string HistoricalImport = "HistoricalImport";
    public const string OutboundAppEcho = "OutboundAppEcho";
}

public static class MetaWebhookEventTypes
{
    public const string InboundText = "InboundText";
    public const string InboundAudio = "InboundAudio";
    public const string InboundImage = "InboundImage";
    public const string DeliveryStatus = "DeliveryStatus";
    public const string UnsupportedMessage = "UnsupportedMessage";
    public const string HistoricalMessage = "HistoricalMessage";
    public const string OutboundAppMessage = "OutboundAppMessage";
}

public static class HistoryImportOutcomes
{
    public const string Imported = "Imported";
    public const string Duplicate = "Duplicate";
    public const string Skipped = "Skipped";
    public const string Failed = "Failed";
}

public sealed record MetaEventProcessingResult(string? HistoryOutcome = null)
{
    public static readonly MetaEventProcessingResult None = new();
}
