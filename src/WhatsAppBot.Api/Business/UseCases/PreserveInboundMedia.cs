using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class PreserveInboundMedia(ILogger<PreserveInboundMedia> logger) : IProcessInboundMedia
{
    public Task ProcessAsync(NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Inbound media event deferred without processing. EventType {EventType}.",
            inboundEvent.EventType);
        return Task.CompletedTask;
    }
}
