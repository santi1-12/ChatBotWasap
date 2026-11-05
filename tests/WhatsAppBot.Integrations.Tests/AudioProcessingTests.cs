using System.Net;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Audio;
using WhatsAppBot.Api.Integrations.OpenAI;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Integrations.Tests;

public sealed class AudioProcessingTests
{
    [Fact]
    public async Task Normalizer_DoesNotPassOggDirectlyToTranscriptionProvider()
    {
        var normalizer = new AudioNormalizationService(Options.Create(new FfmpegOptions()));

        var exception = await Assert.ThrowsAsync<MediaCapabilityUnavailableException>(() =>
            normalizer.NormalizeAsync(
                new MemoryStream([1, 2, 3]), "audio/ogg; codecs=opus", 10, CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.TranscriptionFailed, exception.SafeCode);
    }

    [Fact]
    public async Task Normalizer_RequiresApprovedConversionCapabilityForOtherAudioTypes()
    {
        var normalizer = new AudioNormalizationService(Options.Create(new FfmpegOptions()));

        var exception = await Assert.ThrowsAsync<MediaCapabilityUnavailableException>(() =>
            normalizer.NormalizeAsync(
                new MemoryStream([1, 2, 3]), "audio/mpeg", 10, CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.TranscriptionFailed, exception.SafeCode);
    }

    [Fact]
    public async Task Normalizer_AppliesConfiguredInputLimitToAlreadyNormalizedAudio()
    {
        var normalizer = new AudioNormalizationService(
            Options.Create(new FfmpegOptions { MaxInputBytes = 2 }));

        var exception = await Assert.ThrowsAsync<MediaProcessingException>(() =>
            normalizer.NormalizeAsync(
                new MemoryStream([1, 2, 3]), "audio/ogg", 10, CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.MediaTooLarge, exception.SafeCode);
    }

    [Fact]
    public async Task Normalizer_ConvertsAudioAndRemovesTemporaryDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "WhatsAppBotTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "fake-ffmpeg.cmd");
        await File.WriteAllTextAsync(
            executable,
            "@echo off\r\nsetlocal\r\nset \"input=\"\r\nset \"output=\"\r\n:loop\r\nif \"%~1\"==\"\" goto done\r\nif \"%~1\"==\"-i\" goto input\r\nset \"output=%~1\"\r\nshift\r\ngoto loop\r\n:input\r\nshift\r\nset \"input=%~1\"\r\nshift\r\ngoto loop\r\n:done\r\ncopy /b \"%input%\" \"%output%\" >nul\r\nexit /b 0\r\n");

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "WhatsAppBot");
        var existingDirectories = Directory.Exists(temporaryRoot)
            ? Directory.GetDirectories(temporaryRoot, "audio-*").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        try
        {
            var normalizer = new AudioNormalizationService(Options.Create(new FfmpegOptions
            {
                ExecutablePath = executable,
                MaxInputBytes = 1024,
                MaxOutputBytes = 1024,
                MaxDurationSeconds = 10
            }));

            var result = await normalizer.NormalizeAsync(
                new MemoryStream([1, 2, 3]), "audio/mpeg", 1024, CancellationToken.None);

            Assert.Equal("audio/wav", result.ContentType);
            Assert.Equal([1, 2, 3], result.Content);
            var remainingDirectories = Directory.Exists(temporaryRoot)
                ? Directory.GetDirectories(temporaryRoot, "audio-*")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];
            Assert.Equal(existingDirectories, remainingDirectories);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Normalizer_NonZeroExitReturnsSafeFailureAndCleansTemporaryDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "WhatsAppBotTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "failing-ffmpeg.cmd");
        await File.WriteAllTextAsync(executable, "@echo off\r\nexit /b 1\r\n");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "WhatsAppBot");
        var existingDirectories = Directory.Exists(temporaryRoot)
            ? Directory.GetDirectories(temporaryRoot, "audio-*").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        try
        {
            var normalizer = new AudioNormalizationService(Options.Create(new FfmpegOptions
            {
                ExecutablePath = executable,
                MaxInputBytes = 1024,
                MaxOutputBytes = 1024,
                MaxDurationSeconds = 10
            }));

            var exception = await Assert.ThrowsAsync<MediaProcessingException>(() => normalizer.NormalizeAsync(
                new MemoryStream([1, 2, 3]), "audio/mpeg", 1024, CancellationToken.None));

            Assert.Equal(MessageOutcomeCodes.TranscriptionFailed, exception.SafeCode);
            var remainingDirectories = Directory.Exists(temporaryRoot)
                ? Directory.GetDirectories(temporaryRoot, "audio-*")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];
            Assert.Equal(existingDirectories, remainingDirectories);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OpenAiTranscription_UsesServerSideMultipartRequestAndSafeResponse()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"text\":\"transcribed text\"}")
        });
        var service = new OpenAiAudioTranscriptionService(
            new HttpClient(handler),
            Options.Create(new AiOptions { RequestTimeoutSeconds = 5 }),
            Options.Create(new OpenAiOptions
            {
                ApiKeyReference = "OpenAI:ApiKey",
                TranscriptionModel = "whisper-test",
                TranscriptionEndpoint = "https://api.openai.com/v1/audio/transcriptions"
            }),
            new FakeSecretResolver());

        var result = await service.TranscribeAsync(
            new MemoryStream([1, 2, 3]), "audio/ogg", CancellationToken.None);

        Assert.Equal("transcribed text", result);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("Bearer test-key", handler.Authorization);
        Assert.Contains("whisper-test", handler.Body);
        Assert.Contains("audio.wav", handler.Body);
    }

    [Fact]
    public async Task OpenAiTranscription_ResponseBodyCancellationBecomesSafeTransientFailure()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new FailingContent(new OperationCanceledException())
        });
        var service = new OpenAiAudioTranscriptionService(
            new HttpClient(handler),
            Options.Create(new AiOptions { RequestTimeoutSeconds = 5 }),
            Options.Create(new OpenAiOptions
            {
                ApiKeyReference = "OpenAI:ApiKey",
                TranscriptionModel = "whisper-test",
                TranscriptionEndpoint = "https://api.openai.com/v1/audio/transcriptions"
            }),
            new FakeSecretResolver());

        var exception = await Assert.ThrowsAsync<MediaProcessingException>(() =>
            service.TranscribeAsync(new MemoryStream([1, 2, 3]), "audio/ogg", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.TranscriptionFailed, exception.SafeCode);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task OpenAiTranscription_RejectsOutputAboveConfiguredLimit()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"text\":\"too long\"}")
        });
        var service = new OpenAiAudioTranscriptionService(
            new HttpClient(handler),
            Options.Create(new AiOptions { RequestTimeoutSeconds = 5, MaxOutputCharacters = 4 }),
            Options.Create(new OpenAiOptions
            {
                ApiKeyReference = "OpenAI:ApiKey",
                TranscriptionModel = "whisper-test",
                TranscriptionEndpoint = "https://api.openai.com/v1/audio/transcriptions"
            }),
            new FakeSecretResolver());

        var exception = await Assert.ThrowsAsync<MediaProcessingException>(() =>
            service.TranscribeAsync(new MemoryStream([1, 2, 3]), "audio/ogg", CancellationToken.None));

        Assert.Equal(MessageOutcomeCodes.TranscriptionFailed, exception.SafeCode);
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
