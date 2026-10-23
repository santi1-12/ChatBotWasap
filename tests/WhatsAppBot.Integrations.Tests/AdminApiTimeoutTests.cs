using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WhatsAppBot.Admin.Authentication;
using WhatsAppBot.Admin.Configuration;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Shared.DTOs;

namespace WhatsAppBot.Integrations.Tests;

public sealed class AdminApiTimeoutTests
{
    [Fact]
    public async Task ClientMapsHttpClientTimeoutToControlledException()
    {
        using var httpClient = new HttpClient(new DelayingHandler())
        {
            BaseAddress = new Uri("https://localhost:7001")
        };
        var apiClient = new WhatsAppBotApiClient(
            httpClient,
            new FakeAdminSession(),
            Options.Create(new AdminApiOptions
            {
                BaseUrl = "https://localhost:7001",
                RequestTimeoutSeconds = 1
            }),
            NullLogger<WhatsAppBotApiClient>.Instance);

        var exception = await Assert.ThrowsAsync<AdminApiTimeoutException>(() =>
            apiClient.GetWhatsAppIntegrationAsync(CancellationToken.None));

        Assert.Equal("api/v1/admin/whatsapp/integration", exception.Path);
    }

    private sealed class DelayingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }

    private sealed class FakeAdminSession : IAdminSession
    {
        public void SetApiAccessToken(string accessToken) { }

        public string? GetApiAccessToken() => "test-token";

        public void Clear() { }
    }
}
