using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ProcessInboundImage(
    InboundMediaIngestionService ingestionService,
    IMessageRepository messageRepository,
    IConversationStateRepository conversationStateRepository,
    IImageAnalysisService imageAnalysisService,
    ProcessInboundText textProcessor,
    IOptions<MediaStorageOptions> storageOptions,
    IConversationHandoffService handoffService,
    ILogger<ProcessInboundImage> logger) : IProcessInboundImage
{
    public async Task ProcessAsync(
        NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken)
    {
        var media = await ingestionService.IngestAsync(inboundEvent, cancellationToken);
        if (media is null)
        {
            return;
        }

        var state = await conversationStateRepository.GetStateAsync(
            media.TenantId, media.ConversationId, cancellationToken);
        if (state is null)
        {
            await CompleteAsync(media, MessageOutcomeCodes.BlockingError, cancellationToken);
            return;
        }

        if (state.AttentionMode == AttentionMode.Humano)
        {
            await CompleteAsync(media, MessageOutcomeCodes.ConversationInHumanMode, cancellationToken);
            return;
        }

        if (state.LifecycleStatus == LifecycleStatus.Cerrada)
        {
            await CompleteAsync(media, MessageOutcomeCodes.ConversationClosed, cancellationToken);
            return;
        }

        if (!media.Configuration.IsBotEnabled)
        {
            await handoffService.TryHandoffAsync(
                media.TenantId,
                media.ConversationId,
                ConversationHandoffReasonCodes.BusinessRule,
                cancellationToken);
            await CompleteAsync(media, MessageOutcomeCodes.BotDisabled, cancellationToken);
            return;
        }

        if (media.Configuration.ReplyMode == BotReplyMode.FaqOnly ||
            !storageOptions.Value.EnableImageAnalysis)
        {
            await CompleteAsync(
                media,
                storageOptions.Value.EnableImageAnalysis
                    ? null
                    : MessageOutcomeCodes.ImageAnalysisUnavailable,
                cancellationToken);
            return;
        }

        string analysis;
        try
        {
            await using var image = new MemoryStream(media.Content, writable: false);
            analysis = await imageAnalysisService.AnalyzeAsync(image, media.MimeType, cancellationToken);
            if (string.IsNullOrWhiteSpace(analysis))
            {
                throw new MediaProcessingException(MessageOutcomeCodes.ImageAnalysisUnavailable);
            }
        }
        catch (MediaProcessingException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await CompleteAsync(media, exception.SafeCode, cancellationToken);
            return;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            await CompleteAsync(media, MessageOutcomeCodes.ImageAnalysisUnavailable, cancellationToken);
            logger.LogError(
                "Inbound image analysis failed with {ExceptionType}.",
                exception.GetType().Name);
            return;
        }

        await textProcessor.ProcessPersistedAsync(
            media.Integration,
            media.TenantId,
            media.ConversationId,
            media.MessageId,
            analysis.Trim(),
            media.ProviderTimestampUtc,
            cancellationToken);
    }

    private Task CompleteAsync(
        InboundMediaContext media,
        string? outcomeCode,
        CancellationToken cancellationToken) =>
        messageRepository.SetProcessingOutcomeAsync(
            media.TenantId, media.MessageId, ProcessingState.Completed, outcomeCode, cancellationToken);
}
