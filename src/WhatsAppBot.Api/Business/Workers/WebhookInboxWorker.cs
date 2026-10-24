using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.Workers;

public sealed class WebhookInboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookInboxWorker> logger,
    IOptions<MetaOptions> metaOptions,
    IOptions<WorkerOptions> workerOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var meta = metaOptions.Value;
        var worker = workerOptions.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(worker.PollIntervalSeconds));
        do
        {
            try
            {
                await ProcessBatchAsync(stoppingToken, meta.WorkerBatchSize, worker.ItemTimeoutSeconds);
            }
            catch (SqlException exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Webhook inbox persistence failed with SQL error number {SqlErrorNumber}.",
                    exception.Number);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Webhook inbox worker recovered from {ExceptionType}.",
                    exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchAsync(
        CancellationToken cancellationToken, int batchSize, int itemTimeoutSeconds)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IWebhookInboxRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<MessageProcessingService>();
        var events = await inbox.ClaimBatchAsync(batchSize, cancellationToken);
        foreach (var webhookEvent in events)
        {
            using var itemDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            itemDeadline.CancelAfter(TimeSpan.FromSeconds(itemTimeoutSeconds));
            try
            {
                await processor.ProcessAsync(webhookEvent, itemDeadline.Token);
                await inbox.SetStateAsync(
                    webhookEvent.EventKey, WebhookInboxState.Completed, null, cancellationToken);
            }
            catch (BusinessValidationException)
            {
                await TrySetStateAsync(
                    inbox, webhookEvent.EventKey, WebhookInboxState.Failed, "InvalidNormalizedEvent", cancellationToken);
                logger.LogWarning(
                    "A normalized webhook event was rejected. Event type: {EventType}.",
                    webhookEvent.EventType);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await TrySetStateAsync(
                    inbox, webhookEvent.EventKey, WebhookInboxState.Failed, "ProcessingTimedOut", cancellationToken);
                logger.LogWarning(
                    "Webhook event processing timed out. Event type: {EventType}.",
                    webhookEvent.EventType);
            }
            catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
            {
                await TrySetStateAsync(
                    inbox, webhookEvent.EventKey, WebhookInboxState.Failed, "PersistenceFailure", cancellationToken);
                logger.LogError(
                    "Webhook event processing failed with SQL error number {SqlErrorNumber}.",
                    exception.Number);
            }
            catch (InvalidOperationException)
            {
                await TrySetStateAsync(
                    inbox, webhookEvent.EventKey, WebhookInboxState.Failed, "ProcessingFailure", cancellationToken);
                logger.LogError("Webhook event processing failed due to an invalid application state.");
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                await TrySetStateAsync(
                    inbox, webhookEvent.EventKey, WebhookInboxState.Failed, "ProcessingFailure", cancellationToken);
                logger.LogError(
                    "Webhook event processing failed with {ExceptionType}.",
                    exception.GetType().Name);
            }
        }
    }

    private async Task TrySetStateAsync(
        IWebhookInboxRepository inbox,
        string eventKey,
        WebhookInboxState state,
        string failureCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await inbox.SetStateAsync(eventKey, state, failureCode, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                "Webhook inbox state update failed with {ExceptionType}.",
                exception.GetType().Name);
        }
    }
}
