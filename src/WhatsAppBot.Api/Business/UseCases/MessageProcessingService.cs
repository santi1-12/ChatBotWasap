using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class MessageProcessingService(
    IProcessInboundText inboundTextProcessor,
    IProcessInboundMedia mediaProcessor,
    IProcessMetaDeliveryStatus deliveryStatusProcessor,
    IProcessCoexistenceMessage? coexistenceProcessor = null)
{
    public async Task<MetaEventProcessingResult> ProcessAsync(
        NormalizedMetaEvent webhookEvent, CancellationToken cancellationToken)
    {
        switch (webhookEvent.EventType)
        {
            case MetaWebhookEventTypes.InboundText:
                await inboundTextProcessor.ProcessAsync(webhookEvent, cancellationToken);
                return MetaEventProcessingResult.None;
            case MetaWebhookEventTypes.InboundAudio:
            case MetaWebhookEventTypes.InboundImage:
                await mediaProcessor.ProcessAsync(webhookEvent, cancellationToken);
                return MetaEventProcessingResult.None;
            case MetaWebhookEventTypes.DeliveryStatus:
                await deliveryStatusProcessor.ProcessAsync(webhookEvent, cancellationToken);
                return MetaEventProcessingResult.None;
            case MetaWebhookEventTypes.HistoricalMessage:
            case MetaWebhookEventTypes.OutboundAppMessage:
                if (coexistenceProcessor is null)
                {
                    throw new InvalidOperationException("Coexistence message processing is not registered.");
                }

                return await coexistenceProcessor.ProcessAsync(webhookEvent, cancellationToken);
            default:
                return MetaEventProcessingResult.None;
        }
    }
}
