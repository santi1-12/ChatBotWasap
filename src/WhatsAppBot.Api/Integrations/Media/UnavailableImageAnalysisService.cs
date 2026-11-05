using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.Media;

public sealed class UnavailableImageAnalysisService : IImageAnalysisService
{
    public Task<string> AnalyzeAsync(
        Stream image, string contentType, CancellationToken cancellationToken) =>
        throw new MediaCapabilityUnavailableException(MessageOutcomeCodes.ImageAnalysisUnavailable);
}
