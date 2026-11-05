using Microsoft.Extensions.DependencyInjection;

namespace WhatsAppBot.Api.Integrations.GitHubCopilot;

public static class GitHubCopilotServiceCollectionExtensions
{
    public static IServiceCollection AddGitHubCopilot(this IServiceCollection services)
    {
        services.AddSingleton<ICopilotRuntimeFactory, CopilotRuntimeFactory>();
        services.AddScoped<GitHubCopilotReplyGenerator>();
        return services;
    }
}
