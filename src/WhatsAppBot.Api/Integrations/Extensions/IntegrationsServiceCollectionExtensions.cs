using RestSharp;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Meta;

namespace WhatsAppBot.Api.Integrations.Extensions;

public static class IntegrationsServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IRestClient>(provider =>
        {
            var apiOptions = provider.GetRequiredService<IOptions<ApiOptions>>().Value;
            return new RestClient(new RestClientOptions("https://graph.facebook.com/")
            {
                Timeout = TimeSpan.FromSeconds(apiOptions.RequestTimeoutSeconds)
            });
        });
        services.AddScoped<IMetaCloudApiClient, MetaCloudApiClient>();
        services.AddScoped<IMetaEmbeddedSignupAdapter, MetaEmbeddedSignupAdapter>();
        services.AddScoped<IMetaWhatsAppClient, MetaMessageSender>();
        services.AddSingleton<IMetaWebhookSignatureValidator, MetaWebhookSignatureValidator>();
        services.AddSingleton<IMetaWebhookChallengeValidator, MetaWebhookChallengeValidator>();
        services.AddSingleton<IMetaWebhookEventParser, MetaWebhookEventParser>();
        return services;
    }
}
