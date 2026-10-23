using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RestSharp;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.Meta;

public sealed class MetaCloudApiClient(
    IRestClient restClient,
    IOptions<MetaOptions> options) : IMetaCloudApiClient
{
    public async Task<string> ExchangeAuthorizationCodeAsync(
        string code, CancellationToken cancellationToken)
    {
        var settings = RequireApplicationSettings();
        var request = CreateRequest("oauth/access_token", Method.Get);
        request.AddQueryParameter("client_id", settings.AppId);
        request.AddQueryParameter("client_secret", settings.AppSecret);
        request.AddQueryParameter("code", code);
        var response = await ExecuteAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(response.Content!);
        if (!document.RootElement.TryGetProperty("access_token", out var token) ||
            token.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(token.GetString()))
        {
            throw new MetaApiException("MetaTokenExchangeInvalidResponse", false);
        }

        return token.GetString()!;
    }

    public async Task<MetaPhoneNumberDetails> GetPhoneNumberAsync(
        string accessToken, string phoneNumberId, CancellationToken cancellationToken)
    {
        var request = CreateRequest($"{Uri.EscapeDataString(phoneNumberId)}", Method.Get);
        request.AddQueryParameter("fields", "display_phone_number,verified_name");
        request.AddHeader("Authorization", $"Bearer {accessToken}");
        var response = await ExecuteAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(response.Content!);
        var root = document.RootElement;
        var number = GetRequiredString(root, "display_phone_number");
        var name = root.TryGetProperty("verified_name", out var verifiedName) &&
                   verifiedName.ValueKind == JsonValueKind.String
            ? verifiedName.GetString()
            : null;
        return new MetaPhoneNumberDetails(number, name);
    }

    public async Task<bool> PhoneNumberBelongsToWabaAsync(
        string accessToken, string wabaId, string phoneNumberId, CancellationToken cancellationToken)
    {
        string? after = null;
        for (var page = 0; page < 10; page++)
        {
            var request = CreateRequest($"{Uri.EscapeDataString(wabaId)}/phone_numbers", Method.Get);
            request.AddQueryParameter("fields", "id");
            request.AddQueryParameter("limit", "100");
            if (after is not null)
            {
                request.AddQueryParameter("after", after);
            }
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            var response = await ExecuteAsync(request, cancellationToken);
            using var document = JsonDocument.Parse(response.Content!);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                throw new MetaApiException("MetaInvalidResponse", false);
            }

            if (data.EnumerateArray().Any(number =>
                    number.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.String &&
                    string.Equals(id.GetString(), phoneNumberId, StringComparison.Ordinal)))
            {
                return true;
            }

            after = document.RootElement.TryGetProperty("paging", out var paging) &&
                    paging.TryGetProperty("cursors", out var cursors) &&
                    cursors.TryGetProperty("after", out var cursor) &&
                    cursor.ValueKind == JsonValueKind.String
                ? cursor.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(after))
            {
                return false;
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<MetaPhoneNumberSummary>> GetPhoneNumbersAsync(
        string accessToken, string wabaId, CancellationToken cancellationToken)
    {
        var numbers = new Dictionary<string, MetaPhoneNumberSummary>(StringComparer.Ordinal);
        string? after = null;
        for (var page = 0; page < 10; page++)
        {
            var request = CreateRequest($"{Uri.EscapeDataString(wabaId)}/phone_numbers", Method.Get);
            request.AddQueryParameter("fields", "id");
            request.AddQueryParameter("limit", "100");
            if (after is not null)
            {
                request.AddQueryParameter("after", after);
            }

            request.AddHeader("Authorization", "Bearer " + accessToken);
            var response = await ExecuteAsync(request, cancellationToken);
            using var document = JsonDocument.Parse(response.Content!);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                throw new MetaApiException("MetaInvalidResponse", false);
            }

            foreach (var item in data.EnumerateArray())
            {
                var id = GetRequiredString(item, "id");
                if (id.Length > 64)
                {
                    throw new MetaApiException("MetaInvalidResponse", false);
                }

                numbers.TryAdd(id, new MetaPhoneNumberSummary(id));
            }

            after = document.RootElement.TryGetProperty("paging", out var paging) &&
                    paging.TryGetProperty("cursors", out var cursors) &&
                    cursors.TryGetProperty("after", out var cursor) &&
                    cursor.ValueKind == JsonValueKind.String
                ? cursor.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(after))
            {
                return numbers.Values.ToArray();
            }
        }

        throw new MetaApiException("MetaPhoneNumberListTooLarge", false);
    }

    public async Task RegisterPhoneNumberAsync(
        string accessToken, string phoneNumberId, CancellationToken cancellationToken)
    {
        var pin = options.Value.PhoneNumberRegistrationPin;
        if (pin is null || pin.Length != 6 || pin.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new MetaConfigurationException("Meta:PhoneNumberRegistrationPin must be a six-digit secret.");
        }

        var request = CreateRequest($"{Uri.EscapeDataString(phoneNumberId)}/register", Method.Post);
        request.AddHeader("Authorization", $"Bearer {accessToken}");
        request.AddJsonBody(new { messaging_product = "whatsapp", pin });
        _ = await ExecuteAsync(request, cancellationToken);
    }

    public async Task SubscribeWabaAsync(
        string accessToken, string wabaId, CancellationToken cancellationToken)
    {
        var request = CreateRequest($"{Uri.EscapeDataString(wabaId)}/subscribed_apps", Method.Post);
        request.AddHeader("Authorization", $"Bearer {accessToken}");
        _ = await ExecuteAsync(request, cancellationToken);
    }

    public async Task<MetaMessageSendResult> SendTextAsync(
        string accessToken,
        string phoneNumberId,
        string recipient,
        string text,
        CancellationToken cancellationToken)
    {
        var request = CreateRequest($"{Uri.EscapeDataString(phoneNumberId)}/messages", Method.Post);
        request.AddHeader("Authorization", $"Bearer {accessToken}");
        request.AddJsonBody(new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = recipient,
            type = "text",
            text = new { body = text, preview_url = false }
        });

        var response = await restClient.ExecuteAsync(request, cancellationToken);
        if (!response.IsSuccessful)
        {
            return new MetaMessageSendResult(
                false, null, MapSafeFailureCode(response.StatusCode), IsTransient(response.StatusCode));
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            return new MetaMessageSendResult(false, null, "MetaInvalidResponse", false);
        }

        using var document = JsonDocument.Parse(response.Content);
        var messages = document.RootElement.TryGetProperty("messages", out var values) &&
                       values.ValueKind == JsonValueKind.Array
            ? values
            : default;
        var providerId = messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0 &&
                         messages[0].TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
        return string.IsNullOrWhiteSpace(providerId)
            ? new MetaMessageSendResult(false, null, "MetaInvalidResponse", false)
            : new MetaMessageSendResult(true, providerId, null, false);
    }

    private async Task<RestResponse> ExecuteAsync(RestRequest request, CancellationToken cancellationToken)
    {
        var response = await restClient.ExecuteAsync(request, cancellationToken);
        if (!response.IsSuccessful)
        {
            throw new MetaApiException(
                MapSafeFailureCode(response.StatusCode),
                IsTransient(response.StatusCode),
                response.StatusCode,
                GetSafeMetaRequestId(response));
        }
        if (string.IsNullOrWhiteSpace(response.Content))
        {
            throw new MetaApiException("MetaEmptyResponse", false);
        }

        return response;
    }

    private RestRequest CreateRequest(string resource, Method method)
    {
        var version = options.Value.GraphApiVersion;
        if (string.IsNullOrWhiteSpace(version) ||
            version.Length > 16 ||
            version.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '.')))
        {
            throw new MetaConfigurationException("Meta:GraphApiVersion must contain a supported Graph API version.");
        }

        return new RestRequest($"{version}/{resource}", method);
    }

    private MetaOptions RequireApplicationSettings()
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.AppId) || string.IsNullOrWhiteSpace(settings.AppSecret))
        {
            throw new MetaConfigurationException(
                "Meta:AppId and Meta:AppSecret must be supplied by User Secrets or a secure configuration provider.");
        }

        return settings;
    }

    private static string GetRequiredString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new MetaApiException("MetaInvalidResponse", false);

    private static string MapSafeFailureCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.TooManyRequests => "MetaRateLimited",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "MetaAuthorizationFailed",
            HttpStatusCode.BadRequest => "MetaRequestRejected",
            _ when (int)statusCode >= 500 => "MetaUnavailable",
            _ => "MetaRequestFailed"
        };

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static string? GetSafeMetaRequestId(RestResponse response)
    {
        var header = response.Headers?.FirstOrDefault(item =>
            string.Equals(item.Name, "x-fb-trace-id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name, "x-fb-request-id", StringComparison.OrdinalIgnoreCase));
        var value = header?.Value?.ToString();
        return value is not null &&
               value.Length <= 128 &&
               value.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or ':' or '.')
            ? value
            : null;
    }
}

public sealed class MetaApiException(
    string safeCode,
    bool isTransient,
    HttpStatusCode? statusCode = null,
    string? metaRequestId = null)
    : Exception(safeCode)
{
    public string SafeCode { get; } = safeCode;
    public bool IsTransient { get; } = isTransient;
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? MetaRequestId { get; } = metaRequestId;
}

public sealed class MetaConfigurationException(string message) : Exception(message);
