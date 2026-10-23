using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class GetConversationDetail(
    IWhatsAppIntegrationRepository integrationRepository,
    IMessageRepository messageRepository) : IGetConversationDetail
{
    public const int MessagePageSize = 50;

    public async Task<ConversationDetailRecord> ExecuteAsync(
        long conversationId, long? beforeMessageId, CancellationToken cancellationToken)
    {
        if (conversationId < 1)
        {
            throw new BusinessValidationException("Conversation id must be positive.");
        }

        if (beforeMessageId is <= 0)
        {
            throw new BusinessValidationException("beforeMessageId must be positive when supplied.");
        }

        var integration = await integrationRepository.GetCurrentAsync(cancellationToken);
        return await messageRepository.GetHistoryAsync(
                   integration.IntegrationId,
                   conversationId,
                   beforeMessageId,
                   MessagePageSize,
                   cancellationToken)
               ?? throw new BusinessNotFoundException("Conversation");
    }
}
