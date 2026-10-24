using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.OpenAI;

public sealed class OpenAiReplyGenerator(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ISecretReferenceResolver secretResolver) : IAiReplyGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GenerateReplyAsync(string prompt, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.TextModel) ||
            string.IsNullOrWhiteSpace(settings.ApiKeyReference))
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiConfigurationMissing,
                false,
                ConversationHandoffReasonCodes.BlockingError);
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiUnableToResolve,
                false,
                ConversationHandoffReasonCodes.BotUnableToResolve);
        }

        var apiKey = await secretResolver.ResolveAsync(settings.ApiKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiConfigurationMissing,
                false,
                ConversationHandoffReasonCodes.BlockingError);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model = settings.TextModel,
            messages = new[]
            {
                new { role = "system", content = "Responde únicamente con texto útil y seguro para el cliente." },
                new { role = "user", content = prompt }
            },
            max_tokens = settings.MaxOutputTokens,
            temperature = 0
        }, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiProviderTimeout,
                true,
                ConversationHandoffReasonCodes.BlockingError);
        }
        catch (HttpRequestException)
        {
            throw new AiGenerationException(
                MessageOutcomeCodes.AiProviderUnavailable,
                true,
                ConversationHandoffReasonCodes.BlockingError);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AiGenerationException(
                    response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError
                        ? MessageOutcomeCodes.AiProviderUnavailable
                        : MessageOutcomeCodes.AiInvalidResponse,
                    response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError,
                    ConversationHandoffReasonCodes.BlockingError);
            }

            string payload;
            try
            {
                payload = await response.Content.ReadAsStringAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiGenerationException(
                    MessageOutcomeCodes.AiProviderTimeout,
                    true,
                    ConversationHandoffReasonCodes.BlockingError);
            }
            catch (HttpRequestException)
            {
                throw new AiGenerationException(
                    MessageOutcomeCodes.AiProviderUnavailable,
                    true,
                    ConversationHandoffReasonCodes.BlockingError);
            }
            if (payload.Length > 1_000_000)
            {
                throw new AiGenerationException(
                    MessageOutcomeCodes.AiInvalidResponse,
                    false,
                    ConversationHandoffReasonCodes.BlockingError);
            }

            try
            {
                var completion = JsonSerializer.Deserialize<OpenAiCompletion>(payload, JsonOptions);
                var content = completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new AiGenerationException(
                        MessageOutcomeCodes.AiUnableToResolve,
                        false,
                        ConversationHandoffReasonCodes.BotUnableToResolve);
                }

                if (content.Length > settings.MaxOutputCharacters)
                {
                    throw new AiGenerationException(
                        MessageOutcomeCodes.AiInvalidResponse,
                        false,
                        ConversationHandoffReasonCodes.BlockingError);
                }

                return content;
            }
            catch (JsonException)
            {
                throw new AiGenerationException(
                    MessageOutcomeCodes.AiInvalidResponse,
                    false,
                    ConversationHandoffReasonCodes.BlockingError);
            }
        }
    }

    private sealed record OpenAiCompletion(IReadOnlyList<Choice>? Choices);

    private sealed record Choice(Message? Message);

    private sealed record Message([property: JsonPropertyName("content")] string? Content);
}
