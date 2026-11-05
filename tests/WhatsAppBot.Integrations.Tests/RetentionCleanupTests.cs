using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Workers;
using WhatsAppBot.Api.Infrastructure.MediaStorage;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Integrations.Tests;

public sealed class RetentionCleanupTests
{
    [Fact]
    public async Task CleanupPhysicallyDeletesExpiredMediaAndMarksOnlyAttachmentDeleted()
    {
        var now = DateTimeOffset.UtcNow;
        var root = Path.Combine(Path.GetTempPath(), "WhatsAppBotTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await using var serviceProvider = BuildServiceProvider(root, now, out var repository, out var storage);
        var stored = await storage.StorePrivateAsync(
            new MemoryStream([1, 2, 3]), "audio/wav", CancellationToken.None);
        repository.ExpiredMedia = new MediaAttachmentRecord(
            1,
            repository.TenantId,
            2,
            repository.MessageId,
            "meta-media-id",
            MediaKind.Audio,
            "audio/wav",
            stored.SizeBytes,
            stored.StorageReference,
            MediaAvailabilityState.Available,
            now.AddDays(-1),
            now.AddMinutes(-1));

        using var cancellation = new CancellationTokenSource();
        var worker = new RetentionCleanupWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MediaStorageOptions
            {
                PrivateContainer = root,
                CleanupBatchSize = 1,
                CleanupIntervalMinutes = 60
            }),
            NullLogger<RetentionCleanupWorker>.Instance);

        try
        {
            await worker.StartAsync(cancellation.Token);
            await repository.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Null(await storage.OpenReadPrivateAsync(stored.StorageReference, CancellationToken.None));
            Assert.Equal(repository.TenantId, repository.MarkedTenantId);
            Assert.Equal(repository.MessageId, repository.MarkedMessageId);
            Assert.Equal(1, repository.MarkDeletedCalls);
        }
        finally
        {
            cancellation.Cancel();
            await worker.StopAsync(CancellationToken.None);
            DeleteTemporaryDirectory(root);
        }
    }

    private static ServiceProvider BuildServiceProvider(
        string root,
        DateTimeOffset now,
        out FakeMediaRepository repository,
        out LocalPrivateMediaStorage storage)
    {
        repository = new FakeMediaRepository(Guid.NewGuid(), 42);
        storage = new LocalPrivateMediaStorage(Options.Create(new MediaStorageOptions
        {
            PrivateContainer = root,
            MaxFileBytes = 1024
        }));
        var localRepository = repository;
        var localStorage = storage;
        var services = new ServiceCollection();
        services.AddScoped<IMediaRepository>(_ => localRepository);
        services.AddScoped<IMediaStorage>(_ => localStorage);
        services.AddScoped<IClock>(_ => new FixedClock(now));
        return services.BuildServiceProvider();
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeMediaRepository(Guid tenantId, long messageId) : IMediaRepository
    {
        public Guid TenantId { get; } = tenantId;
        public long MessageId { get; } = messageId;
        public MediaAttachmentRecord? ExpiredMedia { get; set; }
        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Guid? MarkedTenantId { get; private set; }
        public long? MarkedMessageId { get; private set; }
        public int MarkDeletedCalls { get; private set; }

        public Task<MediaAttachmentRecord> UpsertPendingAsync(
            Guid tenantId, long conversationId, long messageId, string metaMediaId,
            MediaKind mediaKind, string? mimeType, DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetAvailableAsync(
            Guid tenantId, long conversationId, long messageId, string storageReference,
            string mimeType, long sizeBytes, DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetFailedAsync(
            Guid tenantId, long conversationId, long messageId, string? storageReference,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AuthorizedMediaRecord?> GetForAccessAsync(
            Guid tenantId, long messageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MediaAttachmentRecord>> ListExpiredAsync(
            DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaAttachmentRecord>>(
                ExpiredMedia is null ? [] : [ExpiredMedia]);

        public Task MarkDeletedAsync(
            Guid tenantId, long messageId, CancellationToken cancellationToken)
        {
            MarkedTenantId = tenantId;
            MarkedMessageId = messageId;
            MarkDeletedCalls++;
            Completed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task UpsertTranscriptPendingAsync(
            Guid tenantId, long conversationId, long messageId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetTranscriptAsync(
            Guid tenantId, long conversationId, long messageId, string? transcriptText,
            TranscriptState state, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
