using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Integrations.OpenAI;

public static class OpenAiServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAi(this IServiceCollection services)
    {
        services.AddHttpClient<OpenAiReplyGenerator>();
        services.AddHttpClient<OpenAiAudioTranscriptionService>();
        services.AddHttpClient<OpenAiImageAnalysisService>();
        return services;
    }
}
