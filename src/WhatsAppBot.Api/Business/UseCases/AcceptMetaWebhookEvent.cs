using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class AcceptMetaWebhookEvent(IWebhookInboxRepository inboxRepository)
    : IAcceptMetaWebhookEvent
{
    public async Task AcceptAsync(
        IReadOnlyList<NormalizedMetaEvent> events, CancellationToken cancellationToken)
    {
        foreach (var webhookEvent in events)
        {
            if (webhookEvent.EventType is not (MetaWebhookEventTypes.InboundText or
                MetaWebhookEventTypes.InboundAudio or
                MetaWebhookEventTypes.InboundImage or
                MetaWebhookEventTypes.DeliveryStatus or
                MetaWebhookEventTypes.UnsupportedMessage))
            {
                throw new BusinessValidationException("The Meta webhook event type is not supported.");
            }

            await inboxRepository.InsertAsync(webhookEvent, cancellationToken);
        }
    }
}
