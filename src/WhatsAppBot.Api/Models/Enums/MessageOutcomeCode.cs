using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Models.Enums;

public static class MessageOutcomeCodeRules
{
    public static bool IsAllowlisted(string? code) => code switch
    {
        MessageOutcomeCodes.MessagingWindowClosed => true,
        MessageOutcomeCodes.TemplateRequired => true,
        MessageOutcomeCodes.BotDisabled => true,
        MessageOutcomeCodes.NoFrequentResponse => true,
        MessageOutcomeCodes.AiFallbackNotConfigured => true,
        MessageOutcomeCodes.AiConfigurationMissing => true,
        MessageOutcomeCodes.AiProviderTimeout => true,
        MessageOutcomeCodes.AiProviderUnavailable => true,
        MessageOutcomeCodes.AiInvalidResponse => true,
        MessageOutcomeCodes.AiUnableToResolve => true,
        MessageOutcomeCodes.UnsupportedMessageType => true,
        MessageOutcomeCodes.MetaSendFailed => true,
        MessageOutcomeCodes.MetaRequestTimedOut => true,
        MessageOutcomeCodes.MetaUnavailable => true,
        MessageOutcomeCodes.MetaCredentialsMissing => true,
        MessageOutcomeCodes.MetaConfigurationMissing => true,
        MessageOutcomeCodes.MetaInvalidResponse => true,
        MessageOutcomeCodes.MetaSecretStoreUnavailable => true,
        MessageOutcomeCodes.MetaRateLimited => true,
        MessageOutcomeCodes.MetaAuthorizationFailed => true,
        MessageOutcomeCodes.MetaRequestRejected => true,
        MessageOutcomeCodes.MetaRequestFailed => true,
        MessageOutcomeCodes.MetaEmptyResponse => true,
        MessageOutcomeCodes.MetaDeliveryFailed => true,
        MessageOutcomeCodes.ConversationInHumanMode => true,
        MessageOutcomeCodes.ConversationClosed => true,
        MessageOutcomeCodes.ConversationVersionConflict => true,
        MessageOutcomeCodes.IntegrationUnavailable => true,
        MessageOutcomeCodes.ManualSendFailed => true,
        MessageOutcomeCodes.ManualReplyRequiresHuman => true,
        MessageOutcomeCodes.ManualReplyNotAuthorized => true,
        MessageOutcomeCodes.BlockingError => true,
        MessageOutcomeCodes.MediaExpired => true,
        MessageOutcomeCodes.MediaDownloadFailed => true,
        MessageOutcomeCodes.MediaStorageFailed => true,
        MessageOutcomeCodes.MediaInvalidMime => true,
        MessageOutcomeCodes.MediaTooLarge => true,
        MessageOutcomeCodes.TranscriptionFailed => true,
        MessageOutcomeCodes.ImageAnalysisUnavailable => true,
        MessageOutcomeCodes.HistoricalImport => true,
        MessageOutcomeCodes.OutboundAppEcho => true,
        MessageOutcomeCodes.HandoffToHuman => true,
        _ => false
    };
}
