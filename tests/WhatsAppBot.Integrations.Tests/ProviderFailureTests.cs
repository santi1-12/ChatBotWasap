using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RestSharp;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Business.Workers;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Meta;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Integrations.Tests;

public sealed class ProviderFailureTests
{
    [Fact]
    public async Task MetaRequestDeadlineBecomesSafeTransientFailure()
    {
        using var httpClient = new HttpClient(new DelayingHandler());
        var restClient = new RestClient(httpClient, new RestClientOptions("https://graph.facebook.com/"));
        var client = new MetaCloudApiClient(
            restClient,
            Options.Create(new MetaOptions
            {
                AppId = "app-id",
                AppSecret = "app-secret",
                GraphApiVersion = "v24.0",
                RequestTimeoutSeconds = 1
            }));

        var exception = await Assert.ThrowsAsync<MetaApiException>(() =>
            client.GetPhoneNumberAsync("business-token", "phone-1", CancellationToken.None));

        Assert.Equal("MetaRequestTimedOut", exception.SafeCode);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task MessageSenderContainsProviderNetworkFailureWithoutSuccess()
    {
        var sender = new MetaMessageSender(
            new FakeIntegrationRepository(),
            new FakeSecretResolver(),
            new FailingMetaCloudClient());

        var result = await sender.SendTextAsync(
            "phone-1", "15551234567", "Hello", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("MetaUnavailable", result.SafeFailureCode);
        Assert.True(result.IsTransient);
    }

    [Fact]
    public async Task WebhookWorkerContainsProcessingFailureAndContinuesBatch()
    {
        var repository = new RecordingWebhookInboxRepository();
        var services = new ServiceCollection();
        services.AddScoped<IWebhookInboxRepository>(_ => repository);
        services.AddScoped<IProcessInboundText, FailingInboundTextProcessor>();
        services.AddScoped<MessageProcessingService>();
        await using var provider = services.BuildServiceProvider();
        var worker = new WebhookInboxWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WebhookInboxWorker>.Instance,
            Options.Create(new MetaOptions { WorkerBatchSize = 2 }),
            Options.Create(new WorkerOptions
            {
                PollIntervalSeconds = 1,
                ItemTimeoutSeconds = 1,
                MaximumAttempts = 2,
                RetryBaseDelaySeconds = 1
            }));

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, repository.StateUpdates.Count);
        Assert.All(repository.StateUpdates, update =>
        {
            Assert.Equal(WebhookInboxState.Failed, update.State);
            Assert.Equal("ProcessingFailure", update.SafeFailureCode);
        });
    }

    private sealed class DelayingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class FakeIntegrationRepository : IWhatsAppIntegrationRepository
    {
        public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppIntegration(
                Guid.NewGuid(), "waba-1", "phone-1", "+15551234567", "Test",
                ConnectionState.Connected, DateTimeOffset.UtcNow, true)
            {
                AccessTokenSecretRef = "secret-reference"
            });

        public Task SaveSignupResultAsync(MetaSignupResult result, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetConnectionStateAsync(
            Guid integrationId,
            ConnectionState state,
            string? safeErrorCode,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSecretResolver : ISecretReferenceResolver
    {
        public ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>("business-token");
    }

    private sealed class FailingMetaCloudClient : IMetaCloudApiClient
    {
        public Task<string> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MetaPhoneNumberDetails> GetPhoneNumberAsync(
            string accessToken, string phoneNumberId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> PhoneNumberBelongsToWabaAsync(
            string accessToken, string wabaId, string phoneNumberId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MetaPhoneNumberSummary>> GetPhoneNumbersAsync(
            string accessToken, string wabaId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RegisterPhoneNumberAsync(
            string accessToken, string phoneNumberId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SubscribeWabaAsync(
            string accessToken, string wabaId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MetaMessageSendResult> SendTextAsync(
            string accessToken,
            string phoneNumberId,
            string recipient,
            string text,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("provider unavailable");
    }

    private sealed class FailingInboundTextProcessor : IProcessInboundText
    {
        public Task ProcessAsync(NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("simulated processing failure");
    }

    private sealed class RecordingWebhookInboxRepository : IWebhookInboxRepository
    {
        private readonly IReadOnlyList<NormalizedMetaEvent> events =
        [
            CreateEvent("event-1"),
            CreateEvent("event-2")
        ];

        public List<(string EventKey, WebhookInboxState State, string? SafeFailureCode)> StateUpdates { get; } = [];

        public Task<bool> InsertAsync(
            NormalizedMetaEvent webhookEvent, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<NormalizedMetaEvent>> ClaimBatchAsync(
            int batchSize, CancellationToken cancellationToken) => Task.FromResult(events);

        public Task SetStateAsync(
            string eventKey,
            WebhookInboxState state,
            string? safeFailureCode,
            CancellationToken cancellationToken)
        {
            StateUpdates.Add((eventKey, state, safeFailureCode));
            return Task.CompletedTask;
        }

        private static NormalizedMetaEvent CreateEvent(string key) => new(
            key,
            MetaWebhookEventTypes.InboundText,
            "phone-1",
            "waba-1",
            "15551234567",
            null,
            key,
            "hello",
            DateTimeOffset.UtcNow,
            null);
    }
}
