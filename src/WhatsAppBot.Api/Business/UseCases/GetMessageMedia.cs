using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class GetMessageMedia(
    ITenantContext tenantContext,
    IMediaRepository mediaRepository,
    IMediaStorage mediaStorage,
    IClock clock) : IGetMessageMedia
{
    public async Task<MediaAccessResult> ExecuteAsync(
        long messageId, CancellationToken cancellationToken)
    {
        if (messageId < 1)
        {
            return new MediaAccessResult(MediaAccessStatus.NotFound, null, null, null);
        }

        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var media = await mediaRepository.GetForAccessAsync(
            tenant.TenantId, messageId, cancellationToken);
        if (media is null)
        {
            return new MediaAccessResult(MediaAccessStatus.NotFound, null, null, null);
        }

        if (media.AvailabilityState == MediaAvailabilityState.Expired ||
            media.IsExpired(clock.UtcNow))
        {
            return new MediaAccessResult(MediaAccessStatus.Expired, null, null, media.SizeBytes);
        }

        if (media.AvailabilityState is MediaAvailabilityState.Pending or
            MediaAvailabilityState.Failed or MediaAvailabilityState.Deleted)
        {
            return new MediaAccessResult(MediaAccessStatus.NotFound, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(media.StorageReference) ||
            string.IsNullOrWhiteSpace(media.MimeType))
        {
            return new MediaAccessResult(MediaAccessStatus.Unavailable, null, null, media.SizeBytes);
        }

        var content = await mediaStorage.OpenReadPrivateAsync(
            media.StorageReference, cancellationToken);
        return content is null
            ? new MediaAccessResult(MediaAccessStatus.Unavailable, null, media.MimeType, media.SizeBytes)
            : new MediaAccessResult(
                MediaAccessStatus.Authorized,
                content,
                media.MimeType,
                media.SizeBytes);
    }
}
