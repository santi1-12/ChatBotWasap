using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RestSharp;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Meta;

namespace WhatsAppBot.Integrations.Tests;

public sealed class MetaCloudApiClientTests
{
    [Fact]
    public async Task SendText_UsesCloudApiBearerHeaderAndOfficialTextShape()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"messages\":[{\"id\":\"wamid.sent-1\"}]}")
        });
        var client = CreateClient(handler);

        var result = await client.SendTextAsync(
            "business-token", "phone-123", "15551234567", "Hello from FAQ", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("wamid.sent-1", result.ProviderMessageId);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("Bearer business-token", handler.Authorization);
        Assert.EndsWith("/v24.0/phone-123/messages", handler.RequestUri!.AbsolutePath);
        Assert.Contains("\"messaging_product\":\"whatsapp\"", handler.Body);
        Assert.Contains("\"type\":\"text\"", handler.Body);
        Assert.Contains("Hello from FAQ", handler.Body);
    }

    [Fact]
    public async Task SendText_MapsProviderFailureWithoutReturningRawProviderDetails()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"message\":\"private provider detail\"}}")
        });
        var client = CreateClient(handler);

        var result = await client.SendTextAsync(
            "business-token", "phone-123", "15551234567", "Hello", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("MetaRequestRejected", result.SafeFailureCode);
        Assert.DoesNotContain("private provider detail", result.SafeFailureCode);
    }

    [Fact]
    public async Task CodeExchange_UsesMetaOAuthEndpointServerSide()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"business-token\"}")
        });
        var client = CreateClient(handler);

        var accessToken = await client.ExchangeAuthorizationCodeAsync("single-use-code", CancellationToken.None);

        Assert.Equal("business-token", accessToken);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Contains("client_secret=app-secret-value", handler.RequestUri!.Query);
        Assert.Contains("code=single-use-code", handler.RequestUri.Query);
        Assert.Empty(handler.Body);
    }

    [Fact]
    public async Task GetPhoneNumbers_ListsWabaNumbersWithBearerAuthorization()
    {
        var handler = new RecordingHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.Query.Contains("after=cursor-1", StringComparison.Ordinal)
                ? "{\"data\":[{\"id\":\"phone-2\"}]}"
                : "{\"data\":[{\"id\":\"phone-1\"}],\"paging\":{\"cursors\":{\"after\":\"cursor-1\"}}}")
        });
        var client = CreateClient(handler);

        var numbers = await client.GetPhoneNumbersAsync(
            "business-token", "waba-123", CancellationToken.None);

        Assert.Equal(["phone-1", "phone-2"], numbers.Select(number => number.PhoneNumberId));
        Assert.Equal("Bearer business-token", handler.Authorization);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/v24.0/waba-123/phone_numbers", handler.RequestUri!.AbsolutePath);
        Assert.Contains("limit=100", handler.RequestUri.Query);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GraphFailurePreservesOnlySafeStatusAndMetaRequestIdentifier()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":{\"message\":\"private detail\"}}")
            };
            response.Headers.Add("x-fb-trace-id", "trace-123");
            return response;
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<MetaApiException>(() =>
            client.GetPhoneNumberAsync("business-token", "phone-123", CancellationToken.None));

        Assert.Equal("MetaRequestRejected", exception.SafeCode);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("trace-123", exception.MetaRequestId);
        Assert.DoesNotContain("private detail", exception.Message);
    }

    private static MetaCloudApiClient CreateClient(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var restClient = new RestClient(httpClient, new RestClientOptions("https://graph.facebook.com/"));
        return new MetaCloudApiClient(restClient, Options.Create(new MetaOptions
        {
            AppId = "public-app-id",
            AppSecret = "app-secret-value",
            GraphApiVersion = "v24.0"
        }));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
