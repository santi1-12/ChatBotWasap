using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Contracts;

public sealed record ManualConversationMessageResult(
    Guid TenantId,
    long MessageId,
    long ConversationId,
    string Text,
    long AdminId,
    DateTimeOffset CreatedAtUtc,
    ProcessingState ProcessingState,
    DeliveryState DeliveryState);
