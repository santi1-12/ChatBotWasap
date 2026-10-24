using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class MessageProcessingService(
    IProcessInboundText inboundTextProcessor,
    IProcessInboundMedia mediaProcessor,
    IProcessMetaDeliveryStatus deliveryStatusProcessor)
{
    public Task ProcessAsync(NormalizedMetaEvent webhookEvent, CancellationToken cancellationToken) =>
        webhookEvent.EventType switch
        {
            MetaWebhookEventTypes.InboundText =>
                inboundTextProcessor.ProcessAsync(webhookEvent, cancellationToken),
            MetaWebhookEventTypes.InboundAudio or MetaWebhookEventTypes.InboundImage =>
                mediaProcessor.ProcessAsync(webhookEvent, cancellationToken),
            MetaWebhookEventTypes.DeliveryStatus =>
                deliveryStatusProcessor.ProcessAsync(webhookEvent, cancellationToken),
            _ => Task.CompletedTask
        };
}
