using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Requests;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ApplyConversationAction(
    IConversationActionRepository repository,
    ITenantContext tenantContext) : IApplyConversationAction
{
    public async Task ApplyAsync(
        long conversationId,
        ConversationActionCommand command,
        CancellationToken cancellationToken)
    {
        if (conversationId < 1 || !Enum.IsDefined(command.Action))
        {
            throw new BusinessValidationException("A valid conversation action is required.");
        }

        if (string.IsNullOrWhiteSpace(command.ExpectedVersion) || command.ExpectedVersion.Length > 128)
        {
            throw new BusinessValidationException("ExpectedVersion is required.");
        }

        ConversationActionRules.Validate(command);
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var result = await repository.ApplyAsync(
            tenant.TenantId,
            conversationId,
            command,
            tenant.AdministratorId,
            cancellationToken);

        if (!result.Found)
        {
            throw new BusinessNotFoundException("Conversation");
        }

        if (result.Conflict || !result.Applied)
        {
            throw new ConversationConflictException();
        }
    }
}

public static class ConversationActionRules
{
    private static readonly HashSet<string> HandoffReasons =
    [
        "CustomerRequestedHuman",
        "BotUnableToResolve",
        "BusinessRule",
        "BlockingError"
    ];

    public static void Validate(ConversationActionCommand command)
    {
        if (command.ReasonCode is { Length: > 100 })
        {
            throw new BusinessValidationException("ReasonCode cannot exceed 100 characters.");
        }

        if (command.Action == ConversationAction.Handoff &&
            !HandoffReasons.Contains(command.ReasonCode ?? string.Empty))
        {
            throw new BusinessValidationException("Handoff requires an allowlisted reason code.");
        }

        if (command.ActorType != ActorType.Admin || command.Origin != Origin.AdminPanel)
        {
            throw new BusinessValidationException("Administrative actions require the AdminPanel actor and origin.");
        }
    }

    public static bool IsAllowlistedHandoffReason(string? reasonCode) =>
        reasonCode is not null && HandoffReasons.Contains(reasonCode);
}
