using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ListConversations(
    IWhatsAppIntegrationRepository integrationRepository,
    IConversationRepository conversationRepository) : IListConversations
{
    public async Task<ConversationPage> ExecuteAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var integration = await integrationRepository.GetCurrentAsync(cancellationToken);
        return await conversationRepository.ListAsync(
            integration.IntegrationId, page, pageSize, cancellationToken);
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new BusinessValidationException(
                "Page must be positive and pageSize must be between 1 and 100.");
        }
    }
}
