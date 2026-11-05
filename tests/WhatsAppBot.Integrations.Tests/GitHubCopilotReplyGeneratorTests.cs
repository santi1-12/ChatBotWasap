using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Integrations.GitHubCopilot;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Integrations.Tests;

public sealed class GitHubCopilotReplyGeneratorTests
{
    [Fact]
    public async Task GenerateReply_UsesConfiguredModelAndReturnsTextWithoutTools()
    {
        var runtime = new FakeRuntime((_, model, _) =>
            Task.FromResult(new CopilotReply("Respuesta segura", "gpt-6-luna")));
        var factory = new FakeFactory(runtime);
        var generator = CreateGenerator(factory);

        var reply = await generator.GenerateReplyAsync("contexto permitido", CancellationToken.None);

        Assert.Equal("Respuesta segura", reply);
        Assert.Equal(1, factory.CreateCalls);
        Assert.Equal("auto", runtime.RequestedModel);
    }

    [Fact]
    public async Task GenerateReply_AuthenticationFailureReturnsConfigurationMissing()
    {
        var factory = new ThrowingFactory(new CopilotAuthenticationException());
        var generator = CreateGenerator(factory);

        var exception = await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateReplyAsync("contexto permitido", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.AiConfigurationMissing, exception.SafeCode);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task GenerateReply_ModelUnavailableReturnsProviderUnavailable()
    {
        var runtime = new FakeRuntime((_, _, _) =>
            Task.FromException<CopilotReply>(new CopilotModelUnavailableException()));
        var generator = CreateGenerator(new FakeFactory(runtime));

        var exception = await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateReplyAsync("contexto permitido", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.AiProviderUnavailable, exception.SafeCode);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task GenerateReply_EmptyResponseReturnsInvalidResponse()
    {
        var runtime = new FakeRuntime((_, _, _) =>
            Task.FromResult(new CopilotReply(" ", "auto")));
        var generator = CreateGenerator(new FakeFactory(runtime));

        var exception = await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateReplyAsync("contexto permitido", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.AiInvalidResponse, exception.SafeCode);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task GenerateReply_RuntimeTimeoutReturnsProviderTimeout()
    {
        var runtime = new FakeRuntime((_, _, cancellationToken) =>
            Task.Run(async () =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new CopilotReply("never", "auto");
            }, cancellationToken));
        var generator = CreateGenerator(
            new FakeFactory(runtime),
            new AiOptions { RequestTimeoutSeconds = 1 });

        var exception = await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateReplyAsync("contexto permitido", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.AiProviderTimeout, exception.SafeCode);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task GenerateReply_CallerCancellationIsNotConvertedToProviderFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runtime = new FakeRuntime((_, _, cancellationToken) =>
            Task.FromCanceled<CopilotReply>(cancellationToken));
        var generator = CreateGenerator(new FakeFactory(runtime));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            generator.GenerateReplyAsync("contexto permitido", cancellation.Token));
    }

    private static GitHubCopilotReplyGenerator CreateGenerator(
        ICopilotRuntimeFactory factory,
        AiOptions? aiOptions = null) =>
        new(
            Options.Create(aiOptions ?? new AiOptions()),
            Options.Create(new GitHubCopilotOptions { Model = "auto", UseLoggedInUser = true }),
            factory,
            NullLogger<GitHubCopilotReplyGenerator>.Instance);

    private sealed class FakeFactory(ICopilotRuntime runtime) : ICopilotRuntimeFactory
    {
        public int CreateCalls { get; private set; }

        public Task<ICopilotRuntime> CreateAsync(
            GitHubCopilotOptions options,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(runtime);
        }
    }

    private sealed class ThrowingFactory(Exception exception) : ICopilotRuntimeFactory
    {
        public Task<ICopilotRuntime> CreateAsync(
            GitHubCopilotOptions options,
            CancellationToken cancellationToken) =>
            Task.FromException<ICopilotRuntime>(exception);
    }

    private sealed class FakeRuntime(
        Func<string, string, CancellationToken, Task<CopilotReply>> generate) : ICopilotRuntime
    {
        public string? RequestedModel { get; private set; }

        public Task<CopilotReply> GenerateReplyAsync(
            string prompt,
            string model,
            CancellationToken cancellationToken)
        {
            RequestedModel = model;
            return generate(prompt, model, cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
