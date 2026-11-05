using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Integrations.Tests;

public sealed class MediaAccessTests
{
    [Fact]
    public async Task Access_ReturnsAuthorizedStreamWithoutExposingStorageReference()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeMediaRepository(new AuthorizedMediaRecord(
            tenantId,
            42,
            7,
            MediaKind.Image,
            "image/jpeg",
            3,
            "media/private-reference",
            MediaAvailabilityState.Available,
            DateTimeOffset.UtcNow.AddDays(1)));
        var useCase = CreateUseCase(tenantId, repository, new FakeMediaStorage());

        var result = await useCase.ExecuteAsync(7, CancellationToken.None);

        Assert.Equal(MediaAccessStatus.Authorized, result.Status);
        Assert.Equal("image/jpeg", result.MimeType);
        Assert.NotNull(result.Content);
        Assert.DoesNotContain("media/private-reference", result.MimeType);
    }

    [Fact]
    public async Task Access_ReturnsExpiredWithoutOpeningPrivateStorage()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeMediaRepository(new AuthorizedMediaRecord(
            tenantId,
            42,
            7,
            MediaKind.Audio,
            "audio/ogg",
            3,
            "media/private-reference",
            MediaAvailabilityState.Available,
            DateTimeOffset.UtcNow.AddMinutes(-1)));
        var storage = new FakeMediaStorage();
        var useCase = CreateUseCase(tenantId, repository, storage);

        var result = await useCase.ExecuteAsync(7, CancellationToken.None);

        Assert.Equal(MediaAccessStatus.Expired, result.Status);
        Assert.Equal(0, storage.OpenCalls);
    }

    [Fact]
    public async Task Access_ReturnsExpiredAfterCleanupHasMarkedAttachmentDeleted()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeMediaRepository(new AuthorizedMediaRecord(
            tenantId,
            42,
            7,
            MediaKind.Image,
            "image/jpeg",
            null,
            null,
            MediaAvailabilityState.Deleted,
            DateTimeOffset.UtcNow.AddMinutes(-1)));
        var storage = new FakeMediaStorage();
        var useCase = CreateUseCase(tenantId, repository, storage);

        var result = await useCase.ExecuteAsync(7, CancellationToken.None);

        Assert.Equal(MediaAccessStatus.Expired, result.Status);
        Assert.Equal(0, storage.OpenCalls);
    }

    [Theory]
    [InlineData(MediaAvailabilityState.Pending)]
    [InlineData(MediaAvailabilityState.Failed)]
    [InlineData(MediaAvailabilityState.Deleted)]
    public async Task Access_HidesNonAvailableMediaStates(
        MediaAvailabilityState availabilityState)
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeMediaRepository(new AuthorizedMediaRecord(
            tenantId,
            42,
            7,
            MediaKind.Image,
            "image/jpeg",
            null,
            null,
            availabilityState,
            DateTimeOffset.UtcNow.AddDays(1)));
        var storage = new FakeMediaStorage();
        var useCase = CreateUseCase(tenantId, repository, storage);

        var result = await useCase.ExecuteAsync(7, CancellationToken.None);

        Assert.Equal(MediaAccessStatus.NotFound, result.Status);
        Assert.Equal(0, storage.OpenCalls);
    }

    private static GetMessageMedia CreateUseCase(
        Guid tenantId, FakeMediaRepository repository, FakeMediaStorage storage) =>
        new(
            new FakeTenantContext(tenantId),
            repository,
            storage,
            new FixedClock(DateTimeOffset.UtcNow));

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public TenantScope? Current => null;

        public Task<TenantScope> GetRequiredAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TenantScope(
                new TenantRecord(tenantId, "Test", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                new TenantMembershipRecord(1, tenantId, 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public int OpenCalls { get; private set; }

        public Task<MediaStorageObject> StorePrivateAsync(
            Stream content, string contentType, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Stream?> OpenReadPrivateAsync(
            string storageReference, CancellationToken cancellationToken)
        {
            OpenCalls++;
            return Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));
        }

        public Task DeletePrivateAsync(string storageReference, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMediaRepository(AuthorizedMediaRecord? media) : IMediaRepository
    {
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
            Task.FromResult(media?.TenantId == tenantId && media.MessageId == messageId ? media : null);

        public Task<IReadOnlyList<MediaAttachmentRecord>> ListExpiredAsync(
            DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkDeletedAsync(
            Guid tenantId, long messageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpsertTranscriptPendingAsync(
            Guid tenantId, long conversationId, long messageId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetTranscriptAsync(
            Guid tenantId, long conversationId, long messageId, string? transcriptText,
            TranscriptState state, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
