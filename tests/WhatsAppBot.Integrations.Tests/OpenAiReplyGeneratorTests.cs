using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.OpenAI;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Integrations.Tests;

public sealed class OpenAiReplyGeneratorTests
{
    [Fact]
    public async Task GenerateReply_UsesConfiguredModelAndSecretReferenceWithoutLoggingSecret()
    {
        var handler = new RecordingHandler(
            "{\"choices\":[{\"message\":{\"content\":\"Respuesta segura\"}}]}");
        using var client = new HttpClient(handler);
        var generator = new OpenAiReplyGenerator(
            client,
            Options.Create(new AiOptions()),
            Options.Create(new OpenAiOptions
            {
                ApiKeyReference = "OpenAI:ApiKey",
                Endpoint = "https://ai.test/v1/chat/completions",
                TextModel = "configured-model"
            }),
            new FakeSecretResolver("secret-value"));

        var reply = await generator.GenerateReplyAsync("contexto permitido", CancellationToken.None);

        Assert.Equal("Respuesta segura", reply);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("secret-value", handler.AuthorizationParameter);
        Assert.Contains("configured-model", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReply_WithoutCredentialReturnsControlledConfigurationFailure()
    {
        var handler = new RecordingHandler("{}");
        using var client = new HttpClient(handler);
        var generator = new OpenAiReplyGenerator(
            client,
            Options.Create(new AiOptions()),
            Options.Create(new OpenAiOptions { TextModel = "configured-model" }),
            new FakeSecretResolver(null));

        var exception = await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateReplyAsync("contexto permitido", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.AiConfigurationMissing, exception.SafeCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateReply_InvalidProviderPayloadReturnsSafeFailure()
    {
        var handler = new RecordingHandler("{\"unexpected\":true}");
        using var client = new HttpClient(handler);
        var generator = new OpenAiReplyGenerator(
            client,
            Options.Create(new AiOptions()),
            Options.Create(new OpenAiOptions { TextModel = "configured-model" }),
            new FakeSecretResolver("secret-value"));

        var exception = await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateReplyAsync("contexto permitido", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.AiUnableToResolve, exception.SafeCode);
    }

    [Fact]
    public async Task GenerateReply_ResponseBodyCancellationBecomesSafeTransientTimeout()
    {
        var handler = new FailingResponseHandler();
        using var client = new HttpClient(handler);
        var generator = new OpenAiReplyGenerator(
            client,
            Options.Create(new AiOptions { RequestTimeoutSeconds = 5 }),
            Options.Create(new OpenAiOptions { TextModel = "configured-model" }),
            new FakeSecretResolver("secret-value"));

        var exception = await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateReplyAsync("contexto permitido", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.AiProviderTimeout, exception.SafeCode);
        Assert.True(exception.IsTransient);
    }

    private sealed class FakeSecretResolver(string? value) : ISecretReferenceResolver
    {
        public ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken) =>
            ValueTask.FromResult(value);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FailingContent(Exception exception) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(exception);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }
    }

    private sealed class FailingResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new FailingContent(new OperationCanceledException())
            });
    }
}
