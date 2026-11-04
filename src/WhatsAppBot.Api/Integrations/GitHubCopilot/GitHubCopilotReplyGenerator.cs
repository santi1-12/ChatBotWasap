using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.GitHubCopilot;

public sealed class GitHubCopilotReplyGenerator(
    IOptions<AiOptions> aiOptions,
    IOptions<GitHubCopilotOptions> copilotOptions,
    ICopilotRuntimeFactory runtimeFactory,
    ILogger<GitHubCopilotReplyGenerator> logger) : IAiReplyGenerator
{
    public async Task<string> GenerateReplyAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiUnableToResolve,
                false,
                ConversationHandoffReasonCodes.BotUnableToResolve);
        }

        var commonSettings = aiOptions.Value;
        var settings = copilotOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiConfigurationMissing,
                false,
                ConversationHandoffReasonCodes.BlockingError);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(commonSettings.RequestTimeoutSeconds));

        ICopilotRuntime? runtime = null;
        try
        {
            runtime = await runtimeFactory.CreateAsync(settings, timeout.Token);
            var reply = await runtime.GenerateReplyAsync(prompt, settings.Model, timeout.Token);
            var content = reply.Content?.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiGenerationException(
                    MessageOutcomeCodes.AiInvalidResponse,
                    false,
                    ConversationHandoffReasonCodes.BlockingError);
            }

            if (content.Length > commonSettings.MaxOutputCharacters)
            {
                throw new AiGenerationException(
                    MessageOutcomeCodes.AiInvalidResponse,
                    false,
                    ConversationHandoffReasonCodes.BlockingError);
            }

            if (!string.IsNullOrWhiteSpace(reply.EffectiveModel))
            {
                logger.LogInformation(
                    "GitHub Copilot generated an AI reply with effective model {ModelId}.",
                    reply.EffectiveModel);
            }

            return content;
        }
        catch (AiGenerationException)
        {
            throw;
        }
        catch (CopilotAuthenticationException)
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiConfigurationMissing,
                false,
                ConversationHandoffReasonCodes.BlockingError);
        }
        catch (CopilotModelUnavailableException)
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiProviderUnavailable,
                false,
                ConversationHandoffReasonCodes.BlockingError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiProviderTimeout,
                true,
                ConversationHandoffReasonCodes.BlockingError);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("GitHub Copilot runtime was unavailable while generating an AI reply.");
            throw new AiGenerationException(
                MessageOutcomeCodes.AiProviderUnavailable,
                true,
                ConversationHandoffReasonCodes.BlockingError);
        }
        finally
        {
            if (runtime is not null)
            {
                try
                {
                    await runtime.DisposeAsync();
                }
                catch (Exception)
                {
                    logger.LogWarning("GitHub Copilot runtime cleanup did not complete normally.");
                }
            }
        }
    }
}
