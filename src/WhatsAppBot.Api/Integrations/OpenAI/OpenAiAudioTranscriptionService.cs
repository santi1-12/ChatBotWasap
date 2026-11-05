using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.OpenAI;

public sealed class OpenAiAudioTranscriptionService(
    HttpClient httpClient,
    IOptions<AiOptions> aiOptions,
    IOptions<OpenAiOptions> options,
    ISecretReferenceResolver secretResolver) : IAudioTranscriptionService
{
    public async Task<string> TranscribeAsync(
        Stream audio, string contentType, CancellationToken cancellationToken)
    {
        var ai = aiOptions.Value;
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.TranscriptionModel) ||
            string.IsNullOrWhiteSpace(settings.ApiKeyReference))
        {
            throw new MediaCapabilityUnavailableException(MessageOutcomeCodes.TranscriptionFailed);
        }

        var apiKey = await secretResolver.ResolveAsync(settings.ApiKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new MediaCapabilityUnavailableException(MessageOutcomeCodes.TranscriptionFailed);
        }

        var bytes = await ReadBoundedAsync(audio, settings.MaxAudioBytes, cancellationToken);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", "audio.wav");
        form.Add(new StringContent(settings.TranscriptionModel), "model");

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.TranscriptionEndpoint)
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(ai.RequestTimeoutSeconds));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed, true);
        }
        catch (HttpRequestException)
        {
            throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed, true);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new MediaProcessingException(
                    MessageOutcomeCodes.TranscriptionFailed,
                    response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError);
            }

            var payload = await response.Content.ReadAsStringAsync(deadline.Token);
            if (payload.Length > 1_000_000)
            {
                throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed);
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                if (!document.RootElement.TryGetProperty("text", out var text) ||
                    text.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(text.GetString()))
                {
                    throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed);
                }

                return text.GetString()!.Trim();
            }
            catch (JsonException)
            {
                throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed);
            }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        long size = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            size += read;
            if (size > maxBytes)
            {
                throw new MediaProcessingException(MessageOutcomeCodes.MediaTooLarge);
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }
}
