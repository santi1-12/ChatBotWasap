using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.ContractTests;

public sealed class TextReplyFlowTests
{
    [Fact]
    public async Task InboundTextWebhookRoutesToTextProcessor()
    {
        var text = new RecordingTextProcessor();
        var service = new MessageProcessingService(
            text,
            new NoOpMediaProcessor(),
            new NoOpDeliveryProcessor());
        var webhookEvent = new NormalizedMetaEvent(
            "event-1", MetaWebhookEventTypes.InboundText, "phone-1", "waba-1", "user-1", null,
            "wamid-1", "hello", DateTimeOffset.UtcNow, null);

        await service.ProcessAsync(webhookEvent, CancellationToken.None);

        Assert.Same(webhookEvent, text.Event);
    }

    [Fact]
    public async Task DeliveryWebhookRoutesToDeliveryReconciliationBoundary()
    {
        var delivery = new RecordingDeliveryProcessor();
        var service = new MessageProcessingService(
            new NoOpTextProcessor(),
            new NoOpMediaProcessor(),
            delivery);
        var webhookEvent = new NormalizedMetaEvent(
            "event-2", MetaWebhookEventTypes.DeliveryStatus, "phone-1", "waba-1", null, null,
            "wamid-1", null, DateTimeOffset.UtcNow, "delivered");

        await service.ProcessAsync(webhookEvent, CancellationToken.None);

        Assert.Same(webhookEvent, delivery.Event);
    }

    private sealed class RecordingTextProcessor : IProcessInboundText
    {
        public NormalizedMetaEvent? Event { get; private set; }
        public Task ProcessAsync(NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken)
        {
            Event = inboundEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDeliveryProcessor : IProcessMetaDeliveryStatus
    {
        public NormalizedMetaEvent? Event { get; private set; }
        public Task ProcessAsync(NormalizedMetaEvent statusEvent, CancellationToken cancellationToken)
        {
            Event = statusEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpTextProcessor : IProcessInboundText
    {
        public Task ProcessAsync(NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpMediaProcessor : IProcessInboundMedia
    {
        public Task ProcessAsync(NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpDeliveryProcessor : IProcessMetaDeliveryStatus
    {
        public Task ProcessAsync(NormalizedMetaEvent statusEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
