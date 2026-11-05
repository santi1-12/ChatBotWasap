using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ProcessInboundMedia(
    IProcessInboundAudio audioProcessor,
    IProcessInboundImage imageProcessor) : IProcessInboundMedia
{
    public Task ProcessAsync(
        NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken) =>
        inboundEvent.EventType switch
        {
            MetaWebhookEventTypes.InboundAudio =>
                audioProcessor.ProcessAsync(inboundEvent, cancellationToken),
            MetaWebhookEventTypes.InboundImage =>
                imageProcessor.ProcessAsync(inboundEvent, cancellationToken),
            _ => throw new BusinessValidationException("Only audio and image events can be processed as media.")
        };
}
