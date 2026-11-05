using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.Media;

public sealed class UnavailableAudioTranscriptionService : IAudioTranscriptionService
{
    public Task<string> TranscribeAsync(
        Stream audio, string contentType, CancellationToken cancellationToken) =>
        throw new MediaCapabilityUnavailableException(MessageOutcomeCodes.TranscriptionFailed);
}
