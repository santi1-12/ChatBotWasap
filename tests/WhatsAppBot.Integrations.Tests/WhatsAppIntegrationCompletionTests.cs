using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppBot.Admin.Pages.Settings.WhatsApp;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Helpers;
using WhatsAppBot.Api.Integrations.Meta;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;
using WhatsAppBot.Shared.Requests;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Integrations.Tests;

public sealed class WhatsAppIntegrationCompletionTests
{
    [Theory]
    [InlineData(WhatsAppOnboardingType.Standard)]
    [InlineData(WhatsAppOnboardingType.Coexistence)]
    public async Task CompleteSignup_ReturnsOnlyPersistedCompleteConnection(WhatsAppOnboardingType onboardingType)
    {
        var repository = new FakeIntegrationRepository();
        var cloud = new FakeMetaCloudApiClient();
        var secretStore = new FakeSecretStore();
        var executionContext = new CompletionExecutionContext();
        var phaseLogger = CreatePhaseLogger(executionContext);
        var adapter = CreateAdapter(cloud, secretStore, repository, phaseLogger, executionContext);
        var service = new WhatsAppIntegrationService(
            repository, adapter, secretStore, phaseLogger, executionContext, CreateApiOptions());

        var integration = await service.CompleteSignupAsync(
            "authorization-code",
            "waba-1",
            onboardingType == WhatsAppOnboardingType.Standard ? "phone-1" : null,
            onboardingType,
            CancellationToken.None);

        Assert.True(integration.HasCompleteConnection);
        Assert.Equal(ConnectionState.Connected, integration.ConnectionState);
        Assert.Equal("secret-reference", integration.AccessTokenSecretRef);
        Assert.Contains(ConnectionState.Configuring, repository.StateChanges);
        Assert.Contains(ConnectionState.Connected, repository.StateChanges);
        Assert.Contains("subscribe-waba", cloud.Calls);
        Assert.Equal(
            onboardingType == WhatsAppOnboardingType.Standard,
            cloud.Calls.Contains("register-phone"));
    }

    [Theory]
    [InlineData(CompletionFailure.SecretStore, "SecretStoreUnavailable")]
    [InlineData(CompletionFailure.Subscription, "MetaRequestRejected")]
    [InlineData(CompletionFailure.NoPhone, "MetaPhoneNumberNotFound")]
    [InlineData(CompletionFailure.AmbiguousPhone, "MetaPhoneNumberSelectionAmbiguous")]
    public async Task CompleteSignup_FailureLeavesIntegrationNotConnected(
        CompletionFailure failure, string expectedCode)
    {
        var repository = new FakeIntegrationRepository();
        var cloud = new FakeMetaCloudApiClient
        {
            FailSubscribe = failure == CompletionFailure.Subscription,
            PhoneNumbers = failure switch
            {
                CompletionFailure.NoPhone => [],
                CompletionFailure.AmbiguousPhone =>
                [
                    new MetaPhoneNumberSummary("phone-1"),
                    new MetaPhoneNumberSummary("phone-2")
                ],
                _ => [new MetaPhoneNumberSummary("phone-1")]
            }
        };
        var secretStore = new FakeSecretStore
        {
            FailStore = failure == CompletionFailure.SecretStore
        };
        var executionContext = new CompletionExecutionContext();
        var phaseLogger = CreatePhaseLogger(executionContext);
        var service = new WhatsAppIntegrationService(
            repository,
            CreateAdapter(cloud, secretStore, repository, phaseLogger, executionContext),
            secretStore,
            phaseLogger,
            executionContext,
            CreateApiOptions());

        var error = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            service.CompleteSignupAsync(
                "authorization-code",
                "waba-1",
                failure is CompletionFailure.NoPhone or CompletionFailure.AmbiguousPhone ? null : "phone-1",
                failure is CompletionFailure.NoPhone or CompletionFailure.AmbiguousPhone
                    ? WhatsAppOnboardingType.Coexistence
                    : WhatsAppOnboardingType.Standard,
                CancellationToken.None));

        Assert.Equal(expectedCode, error.SafeCode);
        var expectedState = failure is CompletionFailure.SecretStore or CompletionFailure.Subscription
            ? ConnectionState.Unknown
            : ConnectionState.Error;
        Assert.Equal(expectedState, repository.Integration.ConnectionState);
        Assert.Equal(
            expectedState == ConnectionState.Unknown ? "CompletionStateUnknown" : expectedCode,
            repository.Integration.LastConnectionErrorCode);
        Assert.False(repository.Integration.HasCompleteConnection);
    }

    [Fact]
    public async Task CompleteSignup_DoesNotReportConnectedWhenPersistenceCannotBeConfirmed()
    {
        var repository = new FakeIntegrationRepository { SkipSignupPersistence = true };
        var cloud = new FakeMetaCloudApiClient();
        var secretStore = new FakeSecretStore();
        var executionContext = new CompletionExecutionContext();
        var phaseLogger = CreatePhaseLogger(executionContext);
        var service = new WhatsAppIntegrationService(
            repository,
            CreateAdapter(cloud, secretStore, repository, phaseLogger, executionContext),
            secretStore,
            phaseLogger,
            executionContext,
            CreateApiOptions());

        var error = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            service.CompleteSignupAsync(
                "authorization-code", "waba-1", "phone-1",
                WhatsAppOnboardingType.Standard, CancellationToken.None));

        Assert.Equal("IntegrationPersistenceNotConfirmed", error.SafeCode);
        Assert.Equal(ConnectionState.Unknown, repository.Integration.ConnectionState);
        Assert.Equal("CompletionStateUnknown", repository.Integration.LastConnectionErrorCode);
        Assert.False(repository.Integration.HasCompleteConnection);
    }

    [Fact]
    public async Task CompleteSignup_CancellationAfterExternalMutationPersistsUnknownState()
    {
        var repository = new FakeIntegrationRepository();
        var cloud = new FakeMetaCloudApiClient { CancelSubscribe = true };
        var secretStore = new FakeSecretStore();
        var executionContext = new CompletionExecutionContext();
        var phaseLogger = CreatePhaseLogger(executionContext);
        var service = new WhatsAppIntegrationService(
            repository,
            CreateAdapter(cloud, secretStore, repository, phaseLogger, executionContext),
            secretStore,
            phaseLogger,
            executionContext,
            CreateApiOptions());

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            service.CompleteSignupAsync(
                "authorization-code", "waba-1", "phone-1",
                WhatsAppOnboardingType.Coexistence, CancellationToken.None));

        Assert.Equal(ConnectionState.Unknown, repository.Integration.ConnectionState);
        Assert.Equal("CompletionStateUnknown", repository.Integration.LastConnectionErrorCode);
    }

    [Fact]
    public async Task CompleteSignup_DoesNotRetryWhenPersistedStateIsUnknown()
    {
        var repository = new FakeIntegrationRepository { StartUnknown = true };
        var cloud = new FakeMetaCloudApiClient();
        var secretStore = new FakeSecretStore();
        var executionContext = new CompletionExecutionContext();
        var phaseLogger = CreatePhaseLogger(executionContext);
        var service = new WhatsAppIntegrationService(
            repository,
            CreateAdapter(cloud, secretStore, repository, phaseLogger, executionContext),
            secretStore,
            phaseLogger,
            executionContext,
            CreateApiOptions());

        var error = await Assert.ThrowsAsync<BusinessValidationException>(() =>
            service.CompleteSignupAsync(
                "single-use-code", "waba-1", "phone-1",
                WhatsAppOnboardingType.Standard, CancellationToken.None));

        Assert.Contains("indeterminate", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cloud.Calls);
        Assert.False(executionContext.AuthorizationCodeAttempted);
    }

    [Fact]
    public async Task Admin_UsesBackendStateAfterCompletionInsteadOfCompletionResponse()
    {
        var apiClient = new FakeAdminApiClient
        {
            CompletionResponse = Success(CreateIntegration(ConnectionState.NotConnected)),
            CurrentResponse = Success(CreateIntegration(ConnectionState.Connected))
        };
        var page = CreatePage(apiClient);

        await page.OnPostCompleteSignupAsync(CancellationToken.None);

        Assert.Equal(1, apiClient.GetIntegrationCalls);
        Assert.True(page.IsConnected);
        Assert.Equal("Conectado", page.ConnectionLabel(page.Integration?.ConnectionState));
        Assert.NotNull(page.SuccessMessage);
        Assert.Null(page.ErrorMessage);
    }

    [Fact]
    public async Task Admin_DoesNotShowConnectedWhenBackendDoesNotConfirmPersistence()
    {
        var apiClient = new FakeAdminApiClient
        {
            CompletionResponse = Success(CreateIntegration(ConnectionState.Connected)),
            CurrentResponse = Success(CreateIntegration(ConnectionState.NotConnected))
        };
        var page = CreatePage(apiClient);

        await page.OnPostCompleteSignupAsync(CancellationToken.None);

        Assert.Equal(1, apiClient.GetIntegrationCalls);
        Assert.False(page.IsConnected);
        Assert.Equal("No conectado", page.ConnectionLabel(page.Integration?.ConnectionState));
        Assert.Null(page.SuccessMessage);
        Assert.Contains("no confirmó", page.ErrorMessage);
    }

    [Fact]
    public async Task Admin_ShowsSafeBackendFailureInsteadOfGenericSignupError()
    {
        var apiClient = new FakeAdminApiClient
        {
            CompletionResponse = ResponseE<WhatsAppIntegrationDTO>.Fail(
                new ApiErrorDTO
                {
                    Code = "ExternalDependencyFailure",
                    Message = "The external service could not complete the request."
                },
                "test"),
            CurrentResponse = Success(CreateIntegration(
                ConnectionState.Error,
                "MetaPhoneNumberSelectionAmbiguous"))
        };
        var page = CreatePage(apiClient);

        await page.OnPostCompleteSignupAsync(CancellationToken.None);

        Assert.False(page.IsConnected);
        Assert.Contains("varios números", page.ErrorMessage);
        Assert.DoesNotContain("external service", page.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_HandlesCompletionTimeoutWithoutShowingUnconfirmedConnection()
    {
        var apiClient = new FakeAdminApiClient
        {
            CompletionException = new AdminApiTimeoutException("api/v1/admin/whatsapp/embedded-signup"),
            CurrentResponse = Success(CreateIntegration(ConnectionState.NotConnected))
        };
        var page = CreatePage(apiClient);

        await page.OnPostCompleteSignupAsync(CancellationToken.None);

        Assert.False(page.IsConnected);
        Assert.Null(page.SuccessMessage);
        Assert.Contains("tardando", page.PendingMessage);
        Assert.Equal(1, apiClient.GetIntegrationCalls);
    }

    [Fact]
    public async Task Admin_TimeoutCanRecoverPersistedStateThroughFollowUpGet()
    {
        var apiClient = new FakeAdminApiClient
        {
            CompletionException = new AdminApiTimeoutException("api/v1/admin/whatsapp/embedded-signup"),
            CurrentResponse = Success(CreateIntegration(ConnectionState.Connected))
        };
        var page = CreatePage(apiClient);

        await page.OnPostCompleteSignupAsync(CancellationToken.None);

        Assert.True(page.IsConnected);
        Assert.Null(page.SuccessMessage);
        Assert.Contains("consulta posterior", page.PendingMessage);
    }

    [Fact]
    public async Task Admin_DistinguishesUnavailableApiFromMissingMetaConfiguration()
    {
        var unavailableClient = new FakeAdminApiClient
        {
            GetException = new HttpRequestException("network failure")
        };
        var unavailablePage = CreatePage(unavailableClient);

        await unavailablePage.OnGetAsync(CancellationToken.None);

        Assert.False(unavailablePage.ShowConfigurationMissing);
        Assert.Contains("contactar", unavailablePage.ErrorMessage);

        var missingConfigClient = new FakeAdminApiClient
        {
            CurrentResponse = Success(CreateIntegration(ConnectionState.NotConnected, includeMetaConfiguration: false))
        };
        var missingConfigPage = CreatePage(missingConfigClient);

        await missingConfigPage.OnGetAsync(CancellationToken.None);

        Assert.True(missingConfigPage.ApiResponseAvailable);
        Assert.True(missingConfigPage.ShowConfigurationMissing);
    }

    private static MetaEmbeddedSignupAdapter CreateAdapter(
        FakeMetaCloudApiClient cloud,
        FakeSecretStore secretStore,
        FakeIntegrationRepository repository,
        CompletionPhaseLogger phaseLogger,
        CompletionExecutionContext executionContext) =>
        new(
            cloud,
            secretStore,
            repository,
            Options.Create(new MetaOptions
            {
                AppId = "app-id",
                AppSecret = "app-secret",
                VerifyToken = "verify-token",
                EmbeddedSignupConfigId = "standard-config",
                CoexistenceEmbeddedSignupConfigId = "coexistence-config"
            }),
            phaseLogger,
            executionContext);

    private static CompletionPhaseLogger CreatePhaseLogger(CompletionExecutionContext executionContext) =>
        new(
            NullLogger<CompletionPhaseLogger>.Instance,
            new HttpContextAccessor(),
            executionContext);

    private static IOptions<ApiOptions> CreateApiOptions() =>
        Options.Create(new ApiOptions { CompletionFailureStateTimeoutSeconds = 5 });

    private static IndexModel CreatePage(FakeAdminApiClient apiClient)
    {
        var page = new IndexModel(apiClient)
        {
            AuthorizationCode = "authorization-code",
            WabaId = "waba-1",
            PhoneNumberId = "phone-1",
            OnboardingType = WhatsAppOnboardingType.Standard
        };
        return page;
    }

    private static ResponseE<WhatsAppIntegrationDTO> Success(WhatsAppIntegrationDTO integration) =>
        ResponseE<WhatsAppIntegrationDTO>.Ok(integration, "test");

    private static WhatsAppIntegrationDTO CreateIntegration(
        ConnectionState state,
        string? connectionErrorCode = null,
        bool includeMetaConfiguration = true) =>
        new()
        {
            WabaId = state == ConnectionState.Connected ? "waba-1" : null,
            PhoneNumberId = state == ConnectionState.Connected ? "phone-1" : null,
            BusinessPhoneNumber = state == ConnectionState.Connected ? "+15551234567" : null,
            ConnectionState = state,
            BotEnabled = false,
            ConnectedAtUtc = state == ConnectionState.Connected ? DateTimeOffset.UtcNow : null,
            HasCredentialReference = state == ConnectionState.Connected,
            MetaAppId = includeMetaConfiguration ? "app-id" : null,
            EmbeddedSignupConfigId = includeMetaConfiguration ? "standard-config" : null,
            MetaGraphApiVersion = includeMetaConfiguration ? "v24.0" : null,
            ConnectionErrorCode = connectionErrorCode
        };

    public enum CompletionFailure
    {
        SecretStore,
        Subscription,
        NoPhone,
        AmbiguousPhone
    }

    private sealed class FakeMetaCloudApiClient : IMetaCloudApiClient
    {
        public List<string> Calls { get; } = [];
        public bool FailSubscribe { get; init; }
        public bool CancelSubscribe { get; init; }
        public IReadOnlyList<MetaPhoneNumberSummary> PhoneNumbers { get; init; } =
            [new MetaPhoneNumberSummary("phone-1")];

        public Task<string> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
        {
            Calls.Add("exchange");
            return Task.FromResult("access-token");
        }

        public Task<MetaPhoneNumberDetails> GetPhoneNumberAsync(
            string accessToken, string phoneNumberId, CancellationToken cancellationToken)
        {
            Calls.Add("get-phone");
            return Task.FromResult(new MetaPhoneNumberDetails("+15551234567", "Business"));
        }

        public Task<bool> PhoneNumberBelongsToWabaAsync(
            string accessToken, string wabaId, string phoneNumberId, CancellationToken cancellationToken)
        {
            Calls.Add("validate-phone");
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<MetaPhoneNumberSummary>> GetPhoneNumbersAsync(
            string accessToken, string wabaId, CancellationToken cancellationToken)
        {
            Calls.Add("list-phones");
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
            if (CancelSubscribe)
            {
                throw new TaskCanceledException();
            }

            return FailSubscribe
                ? throw new MetaApiException("MetaRequestRejected", false)
                : Task.CompletedTask;
        }

        public Task<MetaMessageSendResult> SendTextAsync(
            string accessToken, string phoneNumberId, string recipient, string text,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public bool FailStore { get; init; }

        public Task<string> StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken) =>
            FailStore
                ? throw new IOException("Secret store is unavailable.")
                : Task.FromResult("secret-reference");

        public ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(null);

        public Task DeleteAsync(string secretReference, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeIntegrationRepository : IWhatsAppIntegrationRepository
    {
        public WhatsAppIntegration Integration { get; private set; } = new(
            Guid.NewGuid(), null, null, null, null, ConnectionState.NotConnected, null, false);
        public List<ConnectionState> StateChanges { get; } = [];
        public bool SkipSignupPersistence { get; init; }
        public bool StartUnknown { get; init; }

        public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken)
        {
            if (StartUnknown)
            {
                return Task.FromResult(Integration with
                {
                    ConnectionState = ConnectionState.Unknown,
                    LastConnectionErrorCode = "CompletionStateUnknown"
                });
            }

            return Task.FromResult(Integration);
        }

        public Task SaveSignupResultAsync(MetaSignupResult result, CancellationToken cancellationToken)
        {
            if (!SkipSignupPersistence)
            {
                Integration = Integration with
                {
                    WabaId = result.WabaId,
                    PhoneNumberId = result.PhoneNumberId,
                    BusinessPhoneNumber = result.BusinessPhoneNumber,
                    DisplayName = result.DisplayName,
                    ConnectionState = ConnectionState.Connected,
                    ConnectedAtUtc = DateTimeOffset.UtcNow,
                    IsActive = true,
                    AccessTokenSecretRef = result.AccessTokenSecretRef,
                    MetaAppSecretRef = result.MetaAppSecretRef,
                    WebhookVerifyTokenRef = result.WebhookVerifyTokenRef
                };
                StateChanges.Add(ConnectionState.Connected);
            }
            return Task.CompletedTask;
        }

        public Task SetConnectionStateAsync(
            Guid integrationId,
            ConnectionState state,
            string? safeErrorCode,
            CancellationToken cancellationToken)
        {
            Integration = Integration with
            {
                ConnectionState = state,
                IsActive = state == ConnectionState.Connected,
                LastConnectionErrorCode = safeErrorCode
            };
            StateChanges.Add(state);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeAdminApiClient : IWhatsAppBotApiClient
    {
        public ResponseE<WhatsAppIntegrationDTO>? CompletionResponse { get; init; }
        public Exception? CompletionException { get; init; }
        public ResponseE<WhatsAppIntegrationDTO>? CurrentResponse { get; init; }
        public Exception? GetException { get; init; }
        public int GetIntegrationCalls { get; private set; }

        public Task<ResponseE<WhatsAppIntegrationDTO>?> GetWhatsAppIntegrationAsync(
            CancellationToken cancellationToken)
        {
            GetIntegrationCalls++;
            if (GetException is not null)
            {
                return Task.FromException<ResponseE<WhatsAppIntegrationDTO>?>(GetException);
            }

            return Task.FromResult(CurrentResponse);
        }

        public Task<ResponseE<PagedResultDTO<ConversationSummaryDTO>>?> ListConversationsAsync(
            int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResponseE<ConversationDetailDTO>?> GetConversationAsync(
            long conversationId, long? beforeMessageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResponseE<WhatsAppIntegrationDTO>?> CompleteWhatsAppSignupAsync(
            EmbeddedSignupCompletionRequestDTO request, CancellationToken cancellationToken) =>
            CompletionException is null
                ? Task.FromResult(CompletionResponse)
                : Task.FromException<ResponseE<WhatsAppIntegrationDTO>?>(CompletionException);

        public Task<ResponseE<AuthResponseDTO>?> LoginAsync(
            LoginRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DisconnectWhatsAppAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<BotConfigurationDTO>?> GetBotConfigurationAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<BotConfigurationDTO>?> UpdateBotConfigurationAsync(
            UpdateBotConfigurationRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<PagedResultDTO<FrequentResponseDTO>>?> ListFrequentResponsesAsync(
            int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<FrequentResponseDTO>?> GetFrequentResponseAsync(
            long id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<FrequentResponseDTO>?> CreateFrequentResponseAsync(
            FrequentResponseRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<FrequentResponseDTO>?> UpdateFrequentResponseAsync(
            long id, FrequentResponseRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<bool>?> SetFrequentResponseActiveAsync(
            long id, bool isActive, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DeleteFrequentResponseAsync(long id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
