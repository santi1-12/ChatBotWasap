using System.Net;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Media;
using WhatsAppBot.Api.Integrations.OpenAI;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Integrations.Tests;

public sealed class ImageAnalysisTests
{
    [Fact]
    public async Task OpenAiImageAnalysis_UsesBoundedVisionRequestAndReturnsText()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":\"a product photo\"}}]}")
        });
        var service = new OpenAiImageAnalysisService(
            new HttpClient(handler),
            Options.Create(new AiOptions { RequestTimeoutSeconds = 5 }),
            Options.Create(new OpenAiOptions
            {
                ApiKeyReference = "OpenAI:ApiKey",
                ImageModel = "vision-test",
                Endpoint = "https://api.openai.com/v1/chat/completions"
            }),
            new FakeSecretResolver());

        var result = await service.AnalyzeAsync(
            new MemoryStream([255, 216, 255]), "image/jpeg", CancellationToken.None);

        Assert.Equal("a product photo", result);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("Bearer test-key", handler.Authorization);
        Assert.Contains("vision-test", handler.Body);
        Assert.Contains("data:image/jpeg;base64", handler.Body);
    }

    [Fact]
    public async Task CopilotFallbackIsExplicitlyUnavailableForImageAnalysis()
    {
        var service = new UnavailableImageAnalysisService();

        var exception = await Assert.ThrowsAsync<MediaCapabilityUnavailableException>(() =>
            service.AnalyzeAsync(new MemoryStream([1]), "image/jpeg", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.ImageAnalysisUnavailable, exception.SafeCode);
    }

    [Fact]
    public async Task OpenAiImageAnalysis_ResponseBodyCancellationBecomesSafeTransientFailure()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new FailingContent(new OperationCanceledException())
        });
        var service = new OpenAiImageAnalysisService(
            new HttpClient(handler),
            Options.Create(new AiOptions { RequestTimeoutSeconds = 5 }),
            Options.Create(new OpenAiOptions
            {
                ApiKeyReference = "OpenAI:ApiKey",
                ImageModel = "vision-test",
                Endpoint = "https://api.openai.com/v1/chat/completions"
            }),
            new FakeSecretResolver());

        var exception = await Assert.ThrowsAsync<MediaProcessingException>(() =>
            service.AnalyzeAsync(new MemoryStream([255, 216, 255]), "image/jpeg", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.ImageAnalysisUnavailable, exception.SafeCode);
        Assert.True(exception.IsTransient);
    }

    private sealed class FakeSecretResolver : ISecretReferenceResolver
    {
        public ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>("test-key");
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
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
}
