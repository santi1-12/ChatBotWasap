using RestSharp;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.GitHubCopilot;
using WhatsAppBot.Api.Integrations.Meta;
using WhatsAppBot.Api.Integrations.OpenAI;
using WhatsAppBot.Api.Integrations.Audio;
using WhatsAppBot.Api.Integrations.Media;

namespace WhatsAppBot.Api.Integrations.Extensions;

public static class IntegrationsServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IRestClient>(provider =>
        {
            var metaOptions = provider.GetRequiredService<IOptions<MetaOptions>>().Value;
            return new RestClient(new RestClientOptions("https://graph.facebook.com/")
            {
                Timeout = TimeSpan.FromSeconds(metaOptions.RequestTimeoutSeconds),
                FollowRedirects = false
            });
        });
        services.AddScoped<IMetaCloudApiClient, MetaCloudApiClient>();
        services.AddScoped<IMetaEmbeddedSignupAdapter, MetaEmbeddedSignupAdapter>();
        services.AddScoped<IMetaWhatsAppClient, MetaMessageSender>();
        services.AddSingleton<IMetaWebhookSignatureValidator, MetaWebhookSignatureValidator>();
        services.AddSingleton<IMetaWebhookChallengeValidator, MetaWebhookChallengeValidator>();
        services.AddSingleton<IMetaWebhookEventParser, MetaWebhookEventParser>();
        services.AddOpenAi();
        services.AddScoped<IAudioNormalizer, AudioNormalizationService>();
        services.AddScoped<UnavailableAudioTranscriptionService>();
        services.AddScoped<UnavailableImageAnalysisService>();
        services.AddScoped<IAudioTranscriptionService>(provider =>
            string.Equals(
                provider.GetRequiredService<IOptions<AiOptions>>().Value.Provider,
                AiProviderNames.OpenAI,
                StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<OpenAiAudioTranscriptionService>()
                : provider.GetRequiredService<UnavailableAudioTranscriptionService>());
        services.AddScoped<IImageAnalysisService>(provider =>
            string.Equals(
                provider.GetRequiredService<IOptions<AiOptions>>().Value.Provider,
                AiProviderNames.OpenAI,
                StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<OpenAiImageAnalysisService>()
                : provider.GetRequiredService<UnavailableImageAnalysisService>());
        services.AddGitHubCopilot();
        services.AddScoped<AiReplyGeneratorSelector>();
        services.AddScoped<IAiReplyGenerator>(provider =>
            provider.GetRequiredService<AiReplyGeneratorSelector>());
        return services;
    }
}
