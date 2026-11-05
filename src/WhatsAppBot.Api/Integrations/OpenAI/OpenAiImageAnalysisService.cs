using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.OpenAI;

public sealed class OpenAiImageAnalysisService(
    HttpClient httpClient,
    IOptions<AiOptions> aiOptions,
    IOptions<OpenAiOptions> options,
    ISecretReferenceResolver secretResolver) : IImageAnalysisService
{
    public async Task<string> AnalyzeAsync(
        Stream image, string contentType, CancellationToken cancellationToken)
    {
        var ai = aiOptions.Value;
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ImageModel) ||
            string.IsNullOrWhiteSpace(settings.ApiKeyReference))
        {
            throw new MediaCapabilityUnavailableException(MessageOutcomeCodes.ImageAnalysisUnavailable);
        }

        var normalizedContentType = NormalizeContentType(contentType);
        if (normalizedContentType is null ||
            !normalizedContentType.StartsWith("image/", StringComparison.Ordinal))
        {
            throw new MediaProcessingException(MessageOutcomeCodes.MediaInvalidMime);
        }

        var apiKey = await secretResolver.ResolveAsync(settings.ApiKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new MediaCapabilityUnavailableException(MessageOutcomeCodes.ImageAnalysisUnavailable);
        }

        var bytes = await ReadBoundedAsync(image, settings.MaxImageBytes, cancellationToken);
        var dataUri = $"data:{normalizedContentType};base64,{Convert.ToBase64String(bytes)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model = settings.ImageModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Describe esta imagen de forma breve y útil para responder al cliente." },
                        new { type = "image_url", image_url = new { url = dataUri, detail = "low" } }
                    }
                }
            },
            max_tokens = settings.MaxOutputTokens,
            temperature = 0
        });

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
            throw new MediaProcessingException(MessageOutcomeCodes.ImageAnalysisUnavailable, true);
        }
        catch (HttpRequestException)
        {
            throw new MediaProcessingException(MessageOutcomeCodes.ImageAnalysisUnavailable, true);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new MediaProcessingException(
                    MessageOutcomeCodes.ImageAnalysisUnavailable,
                    response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError);
            }

            var payload = await response.Content.ReadAsStringAsync(deadline.Token);
            if (payload.Length > 1_000_000)
            {
                throw new MediaProcessingException(MessageOutcomeCodes.ImageAnalysisUnavailable);
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                var content = document.RootElement.TryGetProperty("choices", out var choices) &&
                              choices.ValueKind == JsonValueKind.Array &&
                              choices.GetArrayLength() > 0 &&
                              choices[0].TryGetProperty("message", out var message) &&
                              message.TryGetProperty("content", out var value) &&
                              value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(content) || content.Length > ai.MaxOutputCharacters)
                {
                    throw new MediaProcessingException(MessageOutcomeCodes.ImageAnalysisUnavailable);
                }

                return content;
            }
            catch (JsonException)
            {
                throw new MediaProcessingException(MessageOutcomeCodes.ImageAnalysisUnavailable);
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

    private static string? NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType)
            ? null
            : contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
}
