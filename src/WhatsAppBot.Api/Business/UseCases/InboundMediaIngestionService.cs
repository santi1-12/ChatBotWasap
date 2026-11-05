using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Meta;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class InboundMediaIngestionService(
    IWhatsAppIntegrationRepository integrationRepository,
    IContactRepository contactRepository,
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IMediaRepository mediaRepository,
    IBotConfigurationService botConfigurationService,
    IMetaCloudApiClient metaCloudApiClient,
    ISecretReferenceResolver secretResolver,
    IMediaStorage mediaStorage,
    IOptions<MediaStorageOptions> storageOptions,
    IClock clock,
    ILogger<InboundMediaIngestionService> logger)
{
    public async Task<InboundMediaContext?> IngestAsync(
        NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken)
    {
        if (!TryGetMediaKind(inboundEvent, out var mediaKind) ||
            string.IsNullOrWhiteSpace(inboundEvent.MediaId) ||
            string.IsNullOrWhiteSpace(inboundEvent.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(inboundEvent.WhatsAppUserId) ||
            string.IsNullOrWhiteSpace(inboundEvent.ProviderMessageId) ||
            inboundEvent.ProviderTimestampUtc is null)
        {
            throw new BusinessValidationException("Only complete inbound media events can be ingested.");
        }

        var tenantId = inboundEvent.TenantId
            ?? throw new BusinessValidationException("The claimed webhook event has no TenantId.");
        var integrationId = inboundEvent.IntegrationId
            ?? throw new BusinessValidationException("The claimed webhook event has no IntegrationId.");
        var integration = await integrationRepository.GetByPhoneNumberIdAsync(
            tenantId, integrationId, inboundEvent.PhoneNumberId, cancellationToken);
        if (!integration.IsActive ||
            integration.ConnectionState != ConnectionState.Connected ||
            !string.Equals(integration.PhoneNumberId, inboundEvent.PhoneNumberId, StringComparison.Ordinal))
        {
            return null;
        }

        if (integration.TenantId != tenantId)
        {
            throw new InvalidOperationException("The WhatsApp integration does not belong to the claimed Tenant.");
        }
        var contactId = await contactRepository.UpsertWhatsAppAsync(
            integration.IntegrationId,
            tenantId,
            inboundEvent.WhatsAppUserId,
            inboundEvent.ContactDisplayName,
            cancellationToken);
        var configuration = await botConfigurationService.GetForIntegrationAsync(
            tenantId, integration.IntegrationId, cancellationToken);
        var conversationId = await conversationRepository.GetOrCreateAsync(
            integration.IntegrationId,
            tenantId,
            contactId,
            inboundEvent.ProviderTimestampUtc.Value,
            configuration.IsBotEnabled,
            cancellationToken);
        var inboundMessage = await messageRepository.InsertInboundAsync(
            tenantId, integration.IntegrationId, conversationId, inboundEvent, cancellationToken);
        if (!inboundMessage.Inserted)
        {
            return null;
        }

        var expiresAtUtc = clock.UtcNow.AddDays(storageOptions.Value.RetentionDays);
        await mediaRepository.UpsertPendingAsync(
            tenantId,
            conversationId,
            inboundMessage.MessageId,
            inboundEvent.MediaId,
            mediaKind,
            inboundEvent.MediaMimeType,
            expiresAtUtc,
            cancellationToken);

        MediaStorageObject? stored = null;
        try
        {
            if (string.IsNullOrWhiteSpace(integration.AccessTokenSecretRef))
            {
                throw new MediaProcessingException(MessageOutcomeCodes.MediaDownloadFailed);
            }

            var accessToken = await secretResolver.ResolveAsync(
                integration.AccessTokenSecretRef, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new MediaProcessingException(MessageOutcomeCodes.MediaDownloadFailed);
            }

            var download = await metaCloudApiClient.DownloadMediaAsync(
                accessToken,
                inboundEvent.MediaId,
                mediaKind,
                inboundEvent.MediaMimeType,
                storageOptions.Value.MaxFileBytes,
                cancellationToken);
            await using var content = new MemoryStream(download.Content, writable: false);
            stored = await mediaStorage.StorePrivateAsync(
                content, download.MimeType, cancellationToken);
            await mediaRepository.SetAvailableAsync(
                tenantId,
                conversationId,
                inboundMessage.MessageId,
                stored.StorageReference,
                download.MimeType,
                stored.SizeBytes,
                expiresAtUtc,
                cancellationToken);

            return new InboundMediaContext(
                integration,
                configuration,
                tenantId,
                conversationId,
                inboundMessage.MessageId,
                inboundEvent.ProviderTimestampUtc.Value,
                mediaKind,
                download.MimeType,
                download.Content);
        }
        catch (MediaProcessingException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkFailedAsync(
                tenantId,
                conversationId,
                inboundMessage.MessageId,
                exception.SafeCode,
                stored,
                cancellationToken);
            return null;
        }
        catch (MetaApiException) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkFailedAsync(
                tenantId,
                conversationId,
                inboundMessage.MessageId,
                MessageOutcomeCodes.MediaDownloadFailed,
                stored,
                cancellationToken);
            return null;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkFailedAsync(
                tenantId,
                conversationId,
                inboundMessage.MessageId,
                MessageOutcomeCodes.MediaStorageFailed,
                stored,
                cancellationToken);
            logger.LogError(
                "Inbound media ingestion failed with {ExceptionType}.",
                exception.GetType().Name);
            return null;
        }
    }

    private async Task MarkFailedAsync(
        Guid tenantId,
        long conversationId,
        long messageId,
        string outcomeCode,
        MediaStorageObject? stored,
        CancellationToken cancellationToken)
    {
        var cleanupSucceeded = true;
        if (stored is not null)
        {
            try
            {
                await mediaStorage.DeletePrivateAsync(stored.StorageReference, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                cleanupSucceeded = false;
                logger.LogWarning(
                    "Failed to remove an uncommitted private media object of type {ExceptionType}.",
                    exception.GetType().Name);
            }
        }

        await mediaRepository.SetFailedAsync(
            tenantId,
            conversationId,
            messageId,
            cleanupSucceeded ? null : stored?.StorageReference,
            cancellationToken);
        await messageRepository.SetProcessingOutcomeAsync(
            tenantId, messageId, ProcessingState.Failed, outcomeCode, cancellationToken);
    }

    private static bool TryGetMediaKind(
        NormalizedMetaEvent inboundEvent, out MediaKind mediaKind)
    {
        if (inboundEvent.EventType == MetaWebhookEventTypes.InboundAudio &&
            inboundEvent.MessageType == MessageType.Audio)
        {
            mediaKind = MediaKind.Audio;
            return true;
        }

        if (inboundEvent.EventType == MetaWebhookEventTypes.InboundImage &&
            inboundEvent.MessageType == MessageType.Image)
        {
            mediaKind = MediaKind.Image;
            return true;
        }

        mediaKind = default;
        return false;
    }
}
