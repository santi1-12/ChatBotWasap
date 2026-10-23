using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Helpers;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Meta;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Integrations.Tests;

public sealed class MetaEmbeddedSignupTests
{
    [Fact]
    public async Task CompleteSignup_ExchangesAndRegistersServerSideThenReturnsOnlySecretReference()
    {
        var cloud = new FakeMetaCloudApiClient();
        var secretStore = new FakeSecretStore();
        var adapter = CreateAdapter(cloud, secretStore);

        var result = await adapter.CompleteAsync(
            "single-use-code", "waba-1", "phone-1",
            WhatsAppOnboardingType.Standard, CancellationToken.None);

        Assert.Equal("waba-1", result.WabaId);
        Assert.Equal("phone-1", result.PhoneNumberId);
        Assert.Equal("secret-ref-123", result.AccessTokenSecretRef);
        Assert.DoesNotContain("access-token-value", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Equal(["exchange", "verify-waba", "lookup-phone", "register-phone", "subscribe-waba"], cloud.Calls);
        Assert.Equal(1, secretStore.StoreCalls);
    }

    [Fact]
    public async Task CompleteSignup_CoexistenceDiscoversSinglePhoneAndSkipsStandardRegistration()
    {
        var cloud = new FakeMetaCloudApiClient();
        var secretStore = new FakeSecretStore();
        var adapter = CreateAdapter(cloud, secretStore);

        var result = await adapter.CompleteAsync(
            "single-use-code", "waba-1", null,
            WhatsAppOnboardingType.Coexistence, CancellationToken.None);

        Assert.Equal("phone-1", result.PhoneNumberId);
        Assert.Equal(
            ["exchange", "list-phone-numbers", "verify-waba", "lookup-phone", "subscribe-waba"],
            cloud.Calls);
        Assert.DoesNotContain("register-phone", cloud.Calls);
        Assert.Equal(1, secretStore.StoreCalls);
    }

    [Fact]
    public async Task CompleteSignup_RejectsPhoneThatDoesNotBelongToSelectedWaba()
    {
        var cloud = new FakeMetaCloudApiClient { PhoneBelongsToWaba = false };
        var secretStore = new FakeSecretStore();
        var adapter = CreateAdapter(cloud, secretStore);

        var error = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            adapter.CompleteAsync("single-use-code", "other-waba", "phone-1",
                WhatsAppOnboardingType.Standard, CancellationToken.None));

        Assert.Equal("MetaPhoneNumberNotInWaba", error.SafeCode);
        Assert.DoesNotContain("register-phone", cloud.Calls);
        Assert.DoesNotContain("subscribe-waba", cloud.Calls);
        Assert.Equal(0, secretStore.StoreCalls);
    }

    [Fact]
    public async Task CompleteSignup_MapsProviderFailuresToSafeProviderNeutralErrors()
    {
        var cloud = new FakeMetaCloudApiClient { FailExchange = true };
        var adapter = CreateAdapter(cloud, new FakeSecretStore());

        var error = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            adapter.CompleteAsync("single-use-code", "waba-1", "phone-1",
                WhatsAppOnboardingType.Standard, CancellationToken.None));

        Assert.Equal("MetaAuthorizationFailed", error.SafeCode);
    }

    [Fact]
    public async Task CompleteSignup_RejectsCoexistenceWhenWabaHasNoPhoneNumbers()
    {
        var cloud = new FakeMetaCloudApiClient { PhoneNumbers = [] };
        var secretStore = new FakeSecretStore();
        var adapter = CreateAdapter(cloud, secretStore);

        var error = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            adapter.CompleteAsync("single-use-code", "waba-1", null,
                WhatsAppOnboardingType.Coexistence, CancellationToken.None));

        Assert.Equal("MetaPhoneNumberNotFound", error.SafeCode);
        Assert.DoesNotContain("lookup-phone", cloud.Calls);
        Assert.DoesNotContain("register-phone", cloud.Calls);
        Assert.DoesNotContain("subscribe-waba", cloud.Calls);
        Assert.Equal(0, secretStore.StoreCalls);
    }

    [Fact]
    public async Task CompleteSignup_RejectsCoexistenceWhenWabaPhoneSelectionIsAmbiguous()
    {
        var cloud = new FakeMetaCloudApiClient
        {
            PhoneNumbers =
            [
                new MetaPhoneNumberSummary("phone-1"),
                new MetaPhoneNumberSummary("phone-2")
            ]
        };
        var secretStore = new FakeSecretStore();
        var adapter = CreateAdapter(cloud, secretStore);

        var error = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            adapter.CompleteAsync("single-use-code", "waba-1", null,
                WhatsAppOnboardingType.Coexistence, CancellationToken.None));

        Assert.Equal("MetaPhoneNumberSelectionAmbiguous", error.SafeCode);
        Assert.DoesNotContain("lookup-phone", cloud.Calls);
        Assert.DoesNotContain("register-phone", cloud.Calls);
        Assert.DoesNotContain("subscribe-waba", cloud.Calls);
        Assert.Equal(0, secretStore.StoreCalls);
    }

    [Fact]
    public async Task CompleteSignup_CoexistenceValidatesPhoneIdSuppliedByBrowserAgainstWaba()
    {
        var cloud = new FakeMetaCloudApiClient { PhoneBelongsToWaba = false };
        var adapter = CreateAdapter(cloud, new FakeSecretStore());

        var error = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            adapter.CompleteAsync("single-use-code", "waba-1", "untrusted-phone",
                WhatsAppOnboardingType.Coexistence, CancellationToken.None));

        Assert.Equal("MetaPhoneNumberNotInWaba", error.SafeCode);
        Assert.DoesNotContain("lookup-phone", cloud.Calls);
        Assert.DoesNotContain("register-phone", cloud.Calls);
    }

    private static MetaEmbeddedSignupAdapter CreateAdapter(
        FakeMetaCloudApiClient cloud, FakeSecretStore secretStore) =>
        CreateAdapter(cloud, secretStore, new CompletionExecutionContext());

    private static MetaEmbeddedSignupAdapter CreateAdapter(
        FakeMetaCloudApiClient cloud,
        FakeSecretStore secretStore,
        CompletionExecutionContext executionContext) =>
        new(
            cloud,
            secretStore,
            new FakeIntegrationRepository(),
            Options.Create(new MetaOptions
            {
                AppId = "public-app",
                AppSecret = "secret",
                VerifyToken = "verify-secret",
                EmbeddedSignupConfigId = "standard-config",
                CoexistenceEmbeddedSignupConfigId = "coexistence-config"
            }),
            new CompletionPhaseLogger(
                NullLogger<CompletionPhaseLogger>.Instance,
                new HttpContextAccessor(),
                executionContext),
            executionContext);

    private sealed class FakeMetaCloudApiClient : IMetaCloudApiClient
    {
        public List<string> Calls { get; } = [];
        public bool PhoneBelongsToWaba { get; init; } = true;
        public bool FailExchange { get; init; }
        public IReadOnlyList<MetaPhoneNumberSummary> PhoneNumbers { get; init; } =
            [new MetaPhoneNumberSummary("phone-1")];

        public Task<string> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
        {
            Calls.Add("exchange");
            return FailExchange
                ? throw new MetaApiException("MetaAuthorizationFailed", false)
                : Task.FromResult("access-token-value");
        }

        public Task<MetaPhoneNumberDetails> GetPhoneNumberAsync(
            string accessToken, string phoneNumberId, CancellationToken cancellationToken)
        {
            Calls.Add("lookup-phone");
            return Task.FromResult(new MetaPhoneNumberDetails("+15551234567", "Test business"));
        }

        public Task<bool> PhoneNumberBelongsToWabaAsync(
            string accessToken, string wabaId, string phoneNumberId, CancellationToken cancellationToken)
        {
            Calls.Add("verify-waba");
            return Task.FromResult(PhoneBelongsToWaba);
        }

        public Task<IReadOnlyList<MetaPhoneNumberSummary>> GetPhoneNumbersAsync(
            string accessToken, string wabaId, CancellationToken cancellationToken)
        {
            Calls.Add("list-phone-numbers");
            return Task.FromResult(PhoneNumbers);
        }

        public Task RegisterPhoneNumberAsync(
            string accessToken, string phoneNumberId, CancellationToken cancellationToken)
        {
            Calls.Add("register-phone");
            return Task.CompletedTask;
        }

        public Task SubscribeWabaAsync(string accessToken, string wabaId, CancellationToken cancellationToken)
        {
            Calls.Add("subscribe-waba");
            return Task.CompletedTask;
        }

        public Task<MetaMessageSendResult> SendTextAsync(
            string accessToken, string phoneNumberId, string recipient, string text,
            CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public int StoreCalls { get; private set; }
        public Task<string> StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken)
        {
            StoreCalls++;
            Assert.Equal("access-token-value", secretValue);
            return Task.FromResult("secret-ref-123");
        }
        public ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(null);
        public Task DeleteAsync(string secretReference, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeIntegrationRepository : IWhatsAppIntegrationRepository
    {
        private readonly WhatsAppIntegration integration = new(
            Guid.NewGuid(), null, null, null, null, ConnectionState.NotConnected, null, false);
        public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(integration);
        public Task<WhatsAppIntegration> GetByPhoneNumberIdAsync(
            string phoneNumberId, CancellationToken cancellationToken) => GetCurrentAsync(cancellationToken);
        public Task SaveSignupResultAsync(MetaSignupResult result, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task SetConnectionStateAsync(
            Guid integrationId, ConnectionState state, string? safeErrorCode, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
