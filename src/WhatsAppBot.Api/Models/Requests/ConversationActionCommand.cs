using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Requests;

public sealed record ConversationActionCommand(
    ConversationAction Action,
    string? ReasonCode,
    string ExpectedVersion,
    ActorType ActorType = ActorType.Admin,
    Origin Origin = Origin.AdminPanel);

public sealed record ConversationAuditEventCommand(
    string EventType,
    AttentionMode? PreviousAttentionMode,
    AttentionMode? NewAttentionMode,
    LifecycleStatus? PreviousLifecycleStatus,
    LifecycleStatus? NewLifecycleStatus,
    ActorType ActorType,
    Origin Origin,
    long? ActorAdminId,
    string? ReasonCode,
    string? ExpectedVersion,
    string? ResultingVersion);

public sealed record ConversationActionApplyResult(
    bool Applied,
    bool Found,
    bool Conflict,
    string? ResultingVersion);
