using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ProcessInboundAudio(
    InboundMediaIngestionService ingestionService,
    IMediaRepository mediaRepository,
    IMessageRepository messageRepository,
    IAudioNormalizer audioNormalizer,
    IAudioTranscriptionService transcriptionService,
    ProcessInboundText textProcessor,
    IOptions<MediaStorageOptions> storageOptions,
    ILogger<ProcessInboundAudio> logger) : IProcessInboundAudio
{
    public async Task ProcessAsync(
        NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken)
    {
        var media = await ingestionService.IngestAsync(inboundEvent, cancellationToken);
        if (media is null)
        {
            return;
        }

        await mediaRepository.UpsertTranscriptPendingAsync(
            media.TenantId, media.ConversationId, media.MessageId, cancellationToken);

        string transcript;
        try
        {
            await using var source = new MemoryStream(media.Content, writable: false);
            var normalized = await audioNormalizer.NormalizeAsync(
                source,
                media.MimeType,
                storageOptions.Value.MaxFileBytes,
                cancellationToken);
            await using var normalizedAudio = new MemoryStream(normalized.Content, writable: false);
            transcript = await transcriptionService.TranscribeAsync(
                normalizedAudio, normalized.ContentType, cancellationToken);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed);
            }

            await mediaRepository.SetTranscriptAsync(
                media.TenantId,
                media.ConversationId,
                media.MessageId,
                transcript.Trim(),
                TranscriptState.Completed,
                cancellationToken);
        }
        catch (MediaProcessingException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkTranscriptFailedAsync(media, exception.SafeCode, cancellationToken);
            return;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkTranscriptFailedAsync(
                media, MessageOutcomeCodes.TranscriptionFailed, cancellationToken);
            logger.LogError(
                "Inbound audio transcription failed with {ExceptionType}.",
                exception.GetType().Name);
            return;
        }

        await textProcessor.ProcessPersistedAsync(
            media.Integration,
            media.TenantId,
            media.ConversationId,
            media.MessageId,
            transcript,
            media.ProviderTimestampUtc,
            cancellationToken);
    }

    private async Task MarkTranscriptFailedAsync(
        InboundMediaContext media,
        string outcomeCode,
        CancellationToken cancellationToken)
    {
        await mediaRepository.SetTranscriptAsync(
            media.TenantId,
            media.ConversationId,
            media.MessageId,
            null,
            TranscriptState.Failed,
            cancellationToken);
        await messageRepository.SetProcessingOutcomeAsync(
            media.MessageId,
            ProcessingState.Completed,
            outcomeCode == MessageOutcomeCodes.MediaTooLarge
                ? outcomeCode
                : MessageOutcomeCodes.TranscriptionFailed,
            cancellationToken);
    }
}
