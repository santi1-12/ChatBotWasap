using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Contracts;

public sealed record BeginMigrationCommand(
    string? ApprovalReference,
    string? OperationalNotes);

public sealed record MigrationExternalMutationCommand(
    Guid MigrationId,
    TransitionState NextState,
    string? ExternalOperationReferenceId);

public sealed record MigrationUnknownCommand(
    Guid MigrationId,
    string SafeErrorCode,
    string? ExternalOperationReferenceId);
