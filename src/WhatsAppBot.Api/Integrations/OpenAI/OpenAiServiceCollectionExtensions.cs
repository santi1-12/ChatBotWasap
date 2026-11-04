using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Integrations.OpenAI;

public static class OpenAiServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAi(this IServiceCollection services)
    {
        services.AddHttpClient<OpenAiReplyGenerator>();
        return services;
    }
}
