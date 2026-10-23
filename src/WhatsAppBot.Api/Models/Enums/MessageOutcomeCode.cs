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
        MessageOutcomeCodes.UnsupportedMessageType => true,
        MessageOutcomeCodes.MetaSendFailed => true,
        MessageOutcomeCodes.MetaDeliveryFailed => true,
        MessageOutcomeCodes.ConversationInHumanMode => true,
        MessageOutcomeCodes.ConversationClosed => true,
        MessageOutcomeCodes.ConversationVersionConflict => true,
        MessageOutcomeCodes.IntegrationUnavailable => true,
        MessageOutcomeCodes.ManualSendFailed => true,
        MessageOutcomeCodes.ManualReplyRequiresHuman => true,
        MessageOutcomeCodes.ManualReplyNotAuthorized => true,
        MessageOutcomeCodes.BlockingError => true,
        _ => false
    };
}
