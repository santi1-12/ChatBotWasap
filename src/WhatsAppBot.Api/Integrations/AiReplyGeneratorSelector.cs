using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.GitHubCopilot;
using WhatsAppBot.Api.Integrations.OpenAI;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations;

public sealed class AiReplyGeneratorSelector(
    IOptions<AiOptions> options,
    OpenAiReplyGenerator openAi,
    GitHubCopilotReplyGenerator githubCopilot) : IAiReplyGenerator
{
    private readonly string provider = options.Value.Provider;

    public Task<string> GenerateReplyAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.Equals(provider, AiProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase))
        {
            return openAi.GenerateReplyAsync(prompt, cancellationToken);
        }

        if (string.Equals(provider, AiProviderNames.GitHubCopilot, StringComparison.OrdinalIgnoreCase))
        {
            return githubCopilot.GenerateReplyAsync(prompt, cancellationToken);
        }

        throw new AiGenerationException(
            MessageOutcomeCodes.AiConfigurationMissing,
            false,
            ConversationHandoffReasonCodes.BlockingError);
    }
}
