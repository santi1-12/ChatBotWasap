using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ApplyMessageStatus(
    IMessageRepository messageRepository,
    ILogger<ApplyMessageStatus> logger) : IProcessMetaDeliveryStatus
{
    private static readonly HashSet<string> SupportedStatuses =
        ["sent", "delivered", "read", "failed"];

    public async Task ProcessAsync(
        NormalizedMetaEvent statusEvent, CancellationToken cancellationToken)
    {
        if (statusEvent.EventType != MetaWebhookEventTypes.DeliveryStatus ||
            string.IsNullOrWhiteSpace(statusEvent.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(statusEvent.ProviderMessageId) ||
            string.IsNullOrWhiteSpace(statusEvent.DeliveryStatus) ||
            statusEvent.ProviderTimestampUtc is null)
        {
            throw new BusinessValidationException("Only complete Meta delivery statuses can be processed.");
        }

        var status = statusEvent.DeliveryStatus.Trim().ToLowerInvariant();
        if (!SupportedStatuses.Contains(status))
        {
            logger.LogInformation("Ignoring unsupported Meta delivery status {DeliveryStatus}.", status);
            return;
        }

        var result = await messageRepository.ApplyDeliveryStatusAsync(statusEvent, cancellationToken);
        if (!result.Found)
        {
            logger.LogInformation(
                "Meta delivery status did not match a persisted outbound message {ProviderMessageId}.",
                statusEvent.ProviderMessageId);
        }
    }
}
