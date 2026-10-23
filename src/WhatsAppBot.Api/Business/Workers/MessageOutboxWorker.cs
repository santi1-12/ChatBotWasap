using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.Workers;

public sealed class MessageOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MessageOutboxWorker> logger,
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
                await ProcessBatchAsync(stoppingToken, meta.WorkerBatchSize, worker);
            }
            catch (SqlException exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Message outbox persistence failed with SQL error number {SqlErrorNumber}.",
                    exception.Number);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Message outbox worker recovered from {ExceptionType}.",
                    exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchAsync(
        CancellationToken cancellationToken, int batchSize, WorkerOptions workerOptions)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var messages = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<IMetaWhatsAppClient>();
        var policy = scope.ServiceProvider.GetRequiredService<IWhatsAppMessagingPolicy>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var batch = await messages.ClaimOutboxBatchAsync(batchSize, cancellationToken);

        foreach (var item in batch)
        {
            using var itemDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            itemDeadline.CancelAfter(TimeSpan.FromSeconds(workerOptions.ItemTimeoutSeconds));
            try
            {
                var decision = policy.Evaluate(item.LastCustomerMessageAtUtc, clock.UtcNow);
                var result = decision.IsAllowed
                    ? await sender.SendTextAsync(item.PhoneNumberId, item.Recipient, item.Text, itemDeadline.Token)
                    : new MetaMessageSendResult(false, null, decision.OutcomeCode, false);
                await PersistResultAsync(messages, item, result, workerOptions, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await PersistResultAsync(
                    messages,
                    item,
                    new MetaMessageSendResult(false, null, "MetaRequestTimedOut", true),
                    workerOptions,
                    cancellationToken);
            }
            catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Outbound message persistence failed with SQL error number {SqlErrorNumber}.",
                    exception.Number);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                await PersistResultAsync(
                    messages,
                    item,
                    new MetaMessageSendResult(false, null, "OutboundProcessingFailed", true),
                    workerOptions,
                    cancellationToken);
                logger.LogError(
                    "Outbound message processing failed with {ExceptionType}.",
                    exception.GetType().Name);
            }
        }
    }

    private async Task PersistResultAsync(
        IMessageRepository messages,
        OutboundMessageWorkItem item,
        MetaMessageSendResult result,
        WorkerOptions workerOptions,
        CancellationToken cancellationToken)
    {
        var terminal = result.Succeeded || !result.IsTransient ||
                       item.AttemptCount >= workerOptions.MaximumAttempts;
        try
        {
            await messages.SetOutboxResultAsync(item.MessageId, result, terminal, cancellationToken);
            if (!terminal)
            {
                var exponent = Math.Min(item.AttemptCount - 1, 9);
                var delay = Math.Min(
                    3600,
                    workerOptions.RetryBaseDelaySeconds * (1 << Math.Max(exponent, 0)));
                await messages.SetOutboxStateAsync(
                    item.MessageId, MessageOutboxState.Pending, delay, cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                "Outbound message result persistence failed with {ExceptionType}.",
                exception.GetType().Name);
        }
    }
}
