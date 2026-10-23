using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppBot.Admin.Authentication;
using WhatsAppBot.Admin.Configuration;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Requests;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Admin.Services;

public interface IWhatsAppBotApiClient
{
    Task<ResponseE<AuthResponseDTO>?> LoginAsync(LoginRequestDTO request, CancellationToken cancellationToken);
    Task<ResponseE<WhatsAppIntegrationDTO>?> GetWhatsAppIntegrationAsync(CancellationToken cancellationToken);
    Task<ResponseE<WhatsAppIntegrationDTO>?> CompleteWhatsAppSignupAsync(
        EmbeddedSignupCompletionRequestDTO request, CancellationToken cancellationToken);
    Task DisconnectWhatsAppAsync(CancellationToken cancellationToken);
    Task<ResponseE<PagedResultDTO<ConversationSummaryDTO>>?> ListConversationsAsync(
        int page, int pageSize, CancellationToken cancellationToken);
    Task<ResponseE<ConversationDetailDTO>?> GetConversationAsync(
        long conversationId, long? beforeMessageId, CancellationToken cancellationToken);
    Task<ResponseE<ConversationDetailDTO>?> ApplyConversationActionAsync(
        long conversationId, ConversationActionRequestDTO request, CancellationToken cancellationToken);
    Task<ResponseE<MessageDTO>?> SendManualConversationMessageAsync(
        long conversationId, ManualMessageRequestDTO request, CancellationToken cancellationToken);
    Task<ResponseE<BotConfigurationDTO>?> GetBotConfigurationAsync(CancellationToken cancellationToken);
    Task<ResponseE<BotConfigurationDTO>?> UpdateBotConfigurationAsync(
        UpdateBotConfigurationRequestDTO request, CancellationToken cancellationToken);
    Task<ResponseE<PagedResultDTO<FrequentResponseDTO>>?> ListFrequentResponsesAsync(
        int page, int pageSize, CancellationToken cancellationToken);
    Task<ResponseE<FrequentResponseDTO>?> GetFrequentResponseAsync(
        long id, CancellationToken cancellationToken);
    Task<ResponseE<FrequentResponseDTO>?> CreateFrequentResponseAsync(
        FrequentResponseRequestDTO request, CancellationToken cancellationToken);
    Task<ResponseE<FrequentResponseDTO>?> UpdateFrequentResponseAsync(
        long id, FrequentResponseRequestDTO request, CancellationToken cancellationToken);
    Task<ResponseE<bool>?> SetFrequentResponseActiveAsync(
        long id, bool isActive, CancellationToken cancellationToken);
    Task DeleteFrequentResponseAsync(long id, CancellationToken cancellationToken);
}

public sealed class WhatsAppBotApiClient : IWhatsAppBotApiClient
{
    private readonly HttpClient httpClient;
    private readonly IAdminSession adminSession;
    private readonly ILogger<WhatsAppBotApiClient> logger;

    public WhatsAppBotApiClient(
        HttpClient httpClient,
        IAdminSession adminSession,
        IOptions<AdminApiOptions> options,
        ILogger<WhatsAppBotApiClient> logger)
    {
        var timeoutSeconds = options.Value.RequestTimeoutSeconds;
        if (timeoutSeconds is <= 0 or > 120)
        {
            throw new InvalidOperationException("AdminApi:RequestTimeoutSeconds must be between 1 and 120.");
        }

        this.httpClient = httpClient;
        this.adminSession = adminSession;
        this.logger = logger;
        this.httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<ResponseE<AuthResponseDTO>?> LoginAsync(
        LoginRequestDTO request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/admin/auth/login")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        using var response = await SendHttpAsync(message, cancellationToken, "api/v1/admin/auth/login");
        return await response.Content.ReadFromJsonAsync<ResponseE<AuthResponseDTO>>(
            JsonOptions, cancellationToken);
    }

    public Task<ResponseE<BotConfigurationDTO>?> GetBotConfigurationAsync(
        CancellationToken cancellationToken) =>
        GetAsync<BotConfigurationDTO>("api/v1/admin/whatsapp/bot-configuration", cancellationToken);

    public Task<ResponseE<WhatsAppIntegrationDTO>?> GetWhatsAppIntegrationAsync(
        CancellationToken cancellationToken) =>
        GetAsync<WhatsAppIntegrationDTO>("api/v1/admin/whatsapp/integration", cancellationToken);

    public Task<ResponseE<WhatsAppIntegrationDTO>?> CompleteWhatsAppSignupAsync(
        EmbeddedSignupCompletionRequestDTO request, CancellationToken cancellationToken) =>
        SendAsync<WhatsAppIntegrationDTO>(
            HttpMethod.Post, "api/v1/admin/whatsapp/embedded-signup", request, cancellationToken);

    public async Task DisconnectWhatsAppAsync(CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Delete, "api/v1/admin/whatsapp/integration");
        using var response = await SendHttpAsync(
            request, cancellationToken, "api/v1/admin/whatsapp/integration");
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException(
                    "The administrator session is no longer authorized.", null, response.StatusCode);
            }

            var envelope = await ReadEnvelopeAsync<object?>(response, cancellationToken);
            throw new AdminApiException(envelope?.Error?.Message ?? "No se pudo desconectar WhatsApp Business.");
        }
    }

    public Task<ResponseE<PagedResultDTO<ConversationSummaryDTO>>?> ListConversationsAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        GetAsync<PagedResultDTO<ConversationSummaryDTO>>(
            $"api/v1/admin/conversations?page={page}&pageSize={pageSize}", cancellationToken);

    public Task<ResponseE<ConversationDetailDTO>?> GetConversationAsync(
        long conversationId, long? beforeMessageId, CancellationToken cancellationToken)
    {
        var path = $"api/v1/admin/conversations/{conversationId}";
        if (beforeMessageId.HasValue)
        {
            path += $"?beforeMessageId={beforeMessageId.Value}";
        }

        return GetAsync<ConversationDetailDTO>(path, cancellationToken);
    }

    public Task<ResponseE<ConversationDetailDTO>?> ApplyConversationActionAsync(
        long conversationId,
        ConversationActionRequestDTO request,
        CancellationToken cancellationToken) =>
        SendAsync<ConversationDetailDTO>(
            HttpMethod.Post, $"api/v1/admin/conversations/{conversationId}/actions", request, cancellationToken);

    public Task<ResponseE<MessageDTO>?> SendManualConversationMessageAsync(
        long conversationId,
        ManualMessageRequestDTO request,
        CancellationToken cancellationToken) =>
        SendAsync<MessageDTO>(
            HttpMethod.Post, $"api/v1/admin/conversations/{conversationId}/messages", request, cancellationToken);

    public Task<ResponseE<BotConfigurationDTO>?> UpdateBotConfigurationAsync(
        UpdateBotConfigurationRequestDTO request, CancellationToken cancellationToken) =>
        SendAsync<BotConfigurationDTO>(
            HttpMethod.Put, "api/v1/admin/whatsapp/bot-configuration", request, cancellationToken);

    public Task<ResponseE<PagedResultDTO<FrequentResponseDTO>>?> ListFrequentResponsesAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        GetAsync<PagedResultDTO<FrequentResponseDTO>>(
            $"api/v1/admin/frequent-responses?page={page}&pageSize={pageSize}", cancellationToken);

    public Task<ResponseE<FrequentResponseDTO>?> GetFrequentResponseAsync(
        long id, CancellationToken cancellationToken) =>
        GetAsync<FrequentResponseDTO>($"api/v1/admin/frequent-responses/{id}", cancellationToken);

    public Task<ResponseE<FrequentResponseDTO>?> CreateFrequentResponseAsync(
        FrequentResponseRequestDTO request, CancellationToken cancellationToken) =>
        SendAsync<FrequentResponseDTO>(
            HttpMethod.Post, "api/v1/admin/frequent-responses", request, cancellationToken);

    public Task<ResponseE<FrequentResponseDTO>?> UpdateFrequentResponseAsync(
        long id, FrequentResponseRequestDTO request, CancellationToken cancellationToken) =>
        SendAsync<FrequentResponseDTO>(
            HttpMethod.Put, $"api/v1/admin/frequent-responses/{id}", request, cancellationToken);

    public Task<ResponseE<bool>?> SetFrequentResponseActiveAsync(
        long id, bool isActive, CancellationToken cancellationToken) =>
        SendAsync<bool>(
            HttpMethod.Patch, $"api/v1/admin/frequent-responses/{id}/active",
            new FrequentResponseStateDTO { IsActive = isActive }, cancellationToken);

    public async Task DeleteFrequentResponseAsync(long id, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Delete, $"api/v1/admin/frequent-responses/{id}");
        using var response = await SendHttpAsync(
            request, cancellationToken, $"api/v1/admin/frequent-responses/{id}");
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("The administrator session is no longer authorized.", null, response.StatusCode);
        }
        var envelope = await ReadEnvelopeAsync<object?>(response, cancellationToken);
        throw new AdminApiException(envelope?.Error?.Message ?? "No se pudo eliminar la respuesta frecuente.");
    }

    private async Task<ResponseE<T>?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, path);
        using var response = await SendHttpAsync(request, cancellationToken, path);
        return await ReadEnvelopeAsync<T>(response, cancellationToken);
    }

    private async Task<ResponseE<T>?> SendAsync<T>(
        HttpMethod method, string path, object request, CancellationToken cancellationToken)
    {
        using var message = CreateAuthorizedRequest(method, path);
        message.Content = JsonContent.Create(request, options: JsonOptions);
        using var response = await SendHttpAsync(message, cancellationToken, path);
        return await ReadEnvelopeAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendHttpAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        string path)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(path, stopwatch);
            throw new AdminApiTimeoutException(path);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(path, stopwatch);
            throw new AdminApiTimeoutException(path);
        }
    }

    private void LogTimeout(string path, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        logger.LogWarning(
            "Admin API request {TraceId} {Path} {Outcome} after {DurationMs} ms",
            Activity.Current?.TraceId.ToString() ?? "unknown",
            path,
            "Timeout",
            stopwatch.ElapsedMilliseconds);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        var message = new HttpRequestMessage(method, path);
        var accessToken = adminSession.GetApiAccessToken();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return message;
    }

    private static async Task<ResponseE<T>?> ReadEnvelopeAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("The administrator session is no longer authorized.", null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<ResponseE<T>>(
            JsonOptions, cancellationToken);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class AdminApiException(string message) : Exception(message);

public sealed class AdminApiTimeoutException(string path)
    : HttpRequestException("The Admin API request timed out.")
{
    public string Path { get; } = path;
}
