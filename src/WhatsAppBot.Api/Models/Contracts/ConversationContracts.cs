using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Contracts;

public sealed record ConversationSummaryRecord(
    long ConversationId,
    string Contact,
    DateTimeOffset LastMessageAtUtc,
    ConversationStatus Status,
    string? LastMessagePreview)
{
    public Guid? TenantId { get; init; }
    public AttentionMode AttentionMode { get; init; } = AttentionMode.Bot;
    public LifecycleStatus LifecycleStatus { get; init; } = LifecycleStatus.Abierta;
    public long? AssignedAdminId { get; init; }
    public string? Version { get; init; }
}

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
    DateTimeOffset CreatedAtUtc)
{
    public Guid? TenantId { get; init; }
    public ActorType? ActorType { get; init; }
    public Origin? Origin { get; init; }
    public long? CreatedByAdminId { get; init; }
    public long? SourceInboundMessageId { get; init; }
}

public sealed record ConversationDetailRecord(
    ConversationSummaryRecord Conversation,
    IReadOnlyList<ConversationMessageRecord> Messages);
