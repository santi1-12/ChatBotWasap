using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Requests;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.Services;

public interface IConversationHandoffService
{
    Task<bool> TryHandoffAsync(
        Guid tenantId,
        long conversationId,
        string reasonCode,
        CancellationToken cancellationToken);
}

public sealed class ConversationHandoffService(
    IConversationStateRepository stateRepository,
    IConversationActionRepository actionRepository) : IConversationHandoffService
{
    public async Task<bool> TryHandoffAsync(
        Guid tenantId,
        long conversationId,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        if (!ConversationHandoffReasonCodes.IsAllowlisted(reasonCode))
        {
            throw new BusinessValidationException("The automatic handoff reason is not supported.");
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var state = await stateRepository.GetStateAsync(
                tenantId, conversationId, cancellationToken);
            if (state is null || state.AttentionMode == AttentionMode.Humano)
            {
                return false;
            }

            var result = await actionRepository.ApplyAsync(
                tenantId,
                conversationId,
                new ConversationActionCommand(
                    ConversationAction.Handoff,
                    reasonCode,
                    state.Version,
                    ActorType.System,
                    Origin.System),
                null,
                cancellationToken);
            if (result.Applied)
            {
                return true;
            }

            if (!result.Conflict)
            {
                return false;
            }
        }

        return false;
    }
}

public static class ConversationHandoffReasonCodes
{
    public const string CustomerRequestedHuman = "CustomerRequestedHuman";
    public const string BotUnableToResolve = "BotUnableToResolve";
    public const string BusinessRule = "BusinessRule";
    public const string BlockingError = "BlockingError";

    public static bool IsAllowlisted(string? reasonCode) => reasonCode is
        CustomerRequestedHuman or BotUnableToResolve or BusinessRule or BlockingError;
}
