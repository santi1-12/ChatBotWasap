using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;

namespace WhatsAppBot.Api.Business.Workers;

public sealed class RetentionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaStorageOptions> mediaOptions,
    ILogger<RetentionCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = mediaOptions.Value;
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(options.CleanupIntervalMinutes));
        do
        {
            try
            {
                await CleanupBatchAsync(stoppingToken, options.CleanupBatchSize);
            }
            catch (SqlException exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Media retention cleanup failed with SQL error number {SqlErrorNumber}.",
                    exception.Number);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Media retention cleanup recovered from {ExceptionType}.",
                    exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupBatchAsync(CancellationToken cancellationToken, int batchSize)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IMediaStorage>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var expired = await repository.ListExpiredAsync(clock.UtcNow, batchSize, cancellationToken);
        foreach (var media in expired)
        {
            if (string.IsNullOrWhiteSpace(media.StorageReference))
            {
                await repository.MarkDeletedAsync(media.TenantId, media.MessageId, cancellationToken);
                continue;
            }

            try
            {
                await storage.DeletePrivateAsync(media.StorageReference, cancellationToken);
                await repository.MarkDeletedAsync(media.TenantId, media.MessageId, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Media retention cleanup deferred an object after {ExceptionType}.",
                    exception.GetType().Name);
            }
        }
    }
}
