using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Helpers;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;
using System.IO;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class WhatsAppIntegrationService(
    IWhatsAppIntegrationRepository repository,
    IMetaEmbeddedSignupAdapter signupAdapter,
    ISecretStore secretStore,
    CompletionPhaseLogger phaseLogger,
    CompletionExecutionContext executionContext) : IWhatsAppIntegrationService
{
    public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
        repository.GetCurrentAsync(cancellationToken);

    public async Task<WhatsAppIntegration> CompleteSignupAsync(
        string authorizationCode,
        string wabaId,
        string? phoneNumberId,
        WhatsAppOnboardingType onboardingType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode) ||
            string.IsNullOrWhiteSpace(wabaId) ||
            (phoneNumberId is not null && string.IsNullOrWhiteSpace(phoneNumberId)) ||
            !Enum.IsDefined(onboardingType))
        {
            throw new BusinessValidationException("Meta signup must include a valid code, WABA, and onboarding type.");
        }

        var integration = await repository.GetCurrentAsync(cancellationToken);
        if (integration.HasCompleteConnection)
        {
            throw new BusinessValidationException("Disconnect the current WhatsApp number before connecting another.");
        }

        try
        {
            await phaseLogger.RunAsync(
                "IntegrationPersistence",
                () => repository.SetConnectionStateAsync(
                    integration.IntegrationId, ConnectionState.Configuring, null, cancellationToken));

            var result = await signupAdapter.CompleteAsync(
                authorizationCode, wabaId, phoneNumberId, onboardingType, cancellationToken);

            await phaseLogger.RunAsync(
                "IntegrationPersistence",
                () => repository.SaveSignupResultAsync(result, cancellationToken));

            var persistedIntegration = await phaseLogger.RunAsync(
                "PersistenceConfirmation",
                async () =>
                {
                    var persisted = await repository.GetCurrentAsync(cancellationToken);
                    if (!persisted.HasCompleteConnection)
                    {
                        throw new ExternalDependencyException("IntegrationPersistenceNotConfirmed");
                    }

                    return persisted;
                });

            return persistedIntegration;
        }
        catch (ExternalDependencyException exception)
        {
            await TryPersistFailureStateAsync(integration.IntegrationId, exception.SafeCode);
            throw;
        }
        catch (OperationCanceledException)
        {
            var safeCode = executionContext.ExternalMutationMayHaveOccurred
                ? "CompletionStateUnknown"
                : cancellationToken.IsCancellationRequested
                    ? "CompletionCanceled"
                    : "CompletionTimedOut";
            await TryPersistFailureStateAsync(integration.IntegrationId, safeCode);
            throw;
        }
        catch (Exception exception) when (
            exception is System.Data.Common.DbException or InvalidOperationException or TimeoutException)
        {
            await TryPersistFailureStateAsync(integration.IntegrationId, "IntegrationPersistenceFailed");
            throw;
        }
    }

    private async Task TryPersistFailureStateAsync(Guid integrationId, string safeErrorCode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var current = await repository.GetCurrentAsync(timeout.Token);
            if (current.HasCompleteConnection)
            {
                return;
            }

            await repository.SetConnectionStateAsync(
                integrationId, ConnectionState.Error, safeErrorCode, timeout.Token);
        }
        catch (Exception)
        {
            phaseLogger.LogFailure("IntegrationPersistence", "CompletionStatePersistenceFailed");
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var integration = await repository.GetCurrentAsync(cancellationToken);
        await repository.DisconnectAsync(integration.IntegrationId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(integration.AccessTokenSecretRef))
        {
            try
            {
                await secretStore.DeleteAsync(integration.AccessTokenSecretRef, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                throw new ExternalDependencyException("CredentialCleanupFailed");
            }
            catch (UnauthorizedAccessException)
            {
                throw new ExternalDependencyException("CredentialCleanupFailed");
            }
            catch (IOException)
            {
                throw new ExternalDependencyException("CredentialCleanupFailed");
            }
        }
    }
}
