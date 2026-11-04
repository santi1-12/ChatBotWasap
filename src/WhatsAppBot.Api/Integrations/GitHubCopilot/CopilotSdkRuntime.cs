using GitHub.Copilot;
using WhatsAppBot.Api.Infrastructure.Options;

namespace WhatsAppBot.Api.Integrations.GitHubCopilot;

public sealed record CopilotReply(string Content, string? EffectiveModel);

public interface ICopilotRuntimeFactory
{
    Task<ICopilotRuntime> CreateAsync(
        GitHubCopilotOptions options,
        CancellationToken cancellationToken);
}

public interface ICopilotRuntime : IAsyncDisposable
{
    Task<CopilotReply> GenerateReplyAsync(
        string prompt,
        string model,
        CancellationToken cancellationToken);
}

public sealed class CopilotAuthenticationException : Exception;

public sealed class CopilotModelUnavailableException : Exception;

public sealed class CopilotRuntimeFactory : ICopilotRuntimeFactory
{
    public async Task<ICopilotRuntime> CreateAsync(
        GitHubCopilotOptions options,
        CancellationToken cancellationToken)
    {
        var client = new CopilotClient(new CopilotClientOptions
        {
            BaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot"),
            // SDK 1.0.28 disables keytar in Empty mode; keep secure user auth and
            // apply the restrictive Empty-equivalent session settings below.
            Mode = CopilotClientMode.CopilotCli,
            UseLoggedInUser = options.UseLoggedInUser
        });

        try
        {
            await client.StartAsync(cancellationToken);
            var auth = await client.GetAuthStatusAsync(cancellationToken);
            if (!auth.IsAuthenticated)
            {
                throw new CopilotAuthenticationException();
            }

            return new SdkCopilotRuntime(client);
        }
        catch
        {
            try
            {
                await client.DisposeAsync();
            }
            catch
            {
                // Preserve the original startup/authentication failure.
            }

            throw;
        }
    }
}

internal sealed class SdkCopilotRuntime(CopilotClient client) : ICopilotRuntime
{
    public async Task<CopilotReply> GenerateReplyAsync(
        string prompt,
        string model,
        CancellationToken cancellationToken)
    {
        var models = await client.ListModelsAsync(cancellationToken);
        if (!models.Any(candidate => string.Equals(candidate.Id, model, StringComparison.OrdinalIgnoreCase)))
        {
            throw new CopilotModelUnavailableException();
        }

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            AvailableTools = [],
            EnableConfigDiscovery = false,
            EnableOnDemandInstructionDiscovery = false,
            EnableFileChangeTracking = false,
            EnableFileHooks = false,
            EnableHostGitOperations = false,
            EnableSessionStore = false,
            EnableSkills = false,
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            WorkingDirectory = Path.GetTempPath()
        }, cancellationToken);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            cancellationToken: cancellationToken);
        var data = response?.Data;
        return new CopilotReply(data?.Content ?? string.Empty, data?.Model);
    }

    public ValueTask DisposeAsync() => client.DisposeAsync();
}
