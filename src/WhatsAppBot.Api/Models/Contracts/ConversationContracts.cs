using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Contracts;

public sealed record ConversationSummaryRecord(
    long ConversationId,
    string Contact,
    DateTimeOffset LastMessageAtUtc,
    ConversationStatus Status,
    string? LastMessagePreview);

public sealed record ConversationPage(
    IReadOnlyList<ConversationSummaryRecord> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ConversationMessageRecord(
    long MessageId,
    MessageDirection Direction,
    MessageType MessageType,
    string? Text,
    string? Transcription,
    bool MediaAvailable,
    ReplySource? ReplySource,
    ProcessingState ProcessingState,
    DeliveryState? DeliveryState,
    string? OutcomeCode,
    DateTimeOffset CreatedAtUtc);

public sealed record ConversationDetailRecord(
    ConversationSummaryRecord Conversation,
    IReadOnlyList<ConversationMessageRecord> Messages);
