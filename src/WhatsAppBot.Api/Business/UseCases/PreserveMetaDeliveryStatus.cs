using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class PreserveMetaDeliveryStatus(ILogger<PreserveMetaDeliveryStatus> logger)
    : IProcessMetaDeliveryStatus
{
    public Task ProcessAsync(NormalizedMetaEvent statusEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Meta delivery status event deferred for delivery projection. EventType {EventType}.",
            statusEvent.EventType);
        return Task.CompletedTask;
    }
}
