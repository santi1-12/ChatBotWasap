using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Helpers;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;
using Microsoft.Extensions.Options;
using System.IO;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class WhatsAppIntegrationService(
    IWhatsAppIntegrationRepository repository,
    IMetaEmbeddedSignupAdapter signupAdapter,
    ISecretStore secretStore,
    CompletionPhaseLogger phaseLogger,
    CompletionExecutionContext executionContext,
    IOptions<ApiOptions> apiOptions,
    IWhatsAppMigrationService? migrationService = null) : IWhatsAppIntegrationService
{
    public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
        repository.GetCurrentAsync(cancellationToken);

    public async Task<HistoryImportStatusRecord?> GetHistoryImportStatusAsync(
        CancellationToken cancellationToken)
    {
        var integration = await repository.GetCurrentAsync(cancellationToken);
        return integration.TenantId is { } tenantId
            ? await repository.GetLatestHistoryImportAsync(
                tenantId, integration.IntegrationId, cancellationToken)
            : null;
    }

    public async Task<WhatsAppIntegration> CompleteSignupAsync(
        string authorizationCode,
        string wabaId,
        string? phoneNumberId,
        WhatsAppOnboardingType onboardingType,
        CancellationToken cancellationToken,
        bool importHistory = false)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode) ||
            string.IsNullOrWhiteSpace(wabaId) ||
            (phoneNumberId is not null && string.IsNullOrWhiteSpace(phoneNumberId)) ||
            !Enum.IsDefined(onboardingType))
        {
            throw new BusinessValidationException("Meta signup must include a valid code, WABA, and onboarding type.");
        }

        var integration = await repository.GetCurrentAsync(cancellationToken);
        var migration = migrationService is null
            ? null
            : await migrationService.GetStatusAsync(cancellationToken);
        var migrationActive = integration.IsMigrationBlocking;
        if (migrationActive && migration is null)
        {
            throw new BusinessValidationException(
                "The WhatsApp migration state has no journal and requires reconciliation.");
        }

        if ((integration.ConnectionState == ConnectionState.Unknown ||
            string.Equals(integration.LastConnectionErrorCode, "CompletionStateUnknown", StringComparison.Ordinal))
            && !migrationActive)
        {
            throw new BusinessValidationException(
                "The previous Meta completion has an indeterminate state. Reconcile or disconnect it before retrying.");
        }
        if (integration.HasCompleteConnection &&
            !(migrationActive && onboardingType == WhatsAppOnboardingType.Coexistence))
        {
            throw new BusinessValidationException("Disconnect the current WhatsApp number before connecting another.");
        }
        if (migrationActive && onboardingType != WhatsAppOnboardingType.Coexistence)
        {
            throw new BusinessValidationException(
                "Only Coexistence completion is allowed during the approved migration.");
        }
        if (migrationActive && migration!.TransitionState == TransitionState.Unknown)
        {
            throw new BusinessValidationException(
                "The Coexistence migration has an indeterminate external state and requires reconciliation.");
        }
        if (migrationActive &&
            (!migration!.ExternalMutationOccurred ||
             migration.TransitionState is not (
                 TransitionState.AwaitingCoexistenceSignup or TransitionState.Reconciling)))
        {
            throw new BusinessValidationException(
                "The external migration steps must be confirmed before Coexistence signup can continue.");
        }

        var migrationExternalMutationOccurred = migration?.ExternalMutationOccurred == true;

        try
        {
            if (!migrationActive)
            {
                await phaseLogger.RunAsync(
                    "IntegrationPersistence",
                    () => repository.SetConnectionStateAsync(
                        integration.IntegrationId, ConnectionState.Configuring, null, cancellationToken));
            }

            var result = await signupAdapter.CompleteAsync(
                authorizationCode, wabaId, phoneNumberId, onboardingType, cancellationToken, importHistory);

            if (migrationActive)
            {
                await migrationService!.ReconcileSignupAsync(result, cancellationToken);
            }
            else
            {
                await phaseLogger.RunAsync(
                    "IntegrationPersistence",
                    () => repository.SaveSignupResultAsync(result, cancellationToken));
            }

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
            if (migrationActive && migrationService is not null && migration is not null &&
                migrationExternalMutationOccurred)
            {
                await TryMarkMigrationUnknownAsync(migration, exception.SafeCode);
            }
            var safeCode = migrationExternalMutationOccurred || executionContext.ExternalMutationMayHaveOccurred
                ? "CompletionStateUnknown"
                : exception.SafeCode;
            await TryPersistFailureStateAsync(integration.IntegrationId, safeCode);
            throw;
        }
        catch (OperationCanceledException)
        {
            if (migrationActive && migrationService is not null && migration is not null &&
                migrationExternalMutationOccurred)
            {
                await TryMarkMigrationUnknownAsync(migration, "CompletionCanceled");
            }
            var safeCode = migrationExternalMutationOccurred || executionContext.ExternalMutationMayHaveOccurred
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
            if (migrationActive && migrationService is not null && migration is not null &&
                migrationExternalMutationOccurred)
            {
                await TryMarkMigrationUnknownAsync(migration, "CompletionStateUnknown");
            }
            var safeCode = migrationExternalMutationOccurred || executionContext.ExternalMutationMayHaveOccurred
                ? "CompletionStateUnknown"
                : "IntegrationPersistenceFailed";
            await TryPersistFailureStateAsync(integration.IntegrationId, safeCode);
            throw;
        }
    }

    private async Task TryMarkMigrationUnknownAsync(
        WhatsAppMigrationStatus migration, string safeErrorCode)
    {
        try
        {
            await migrationService!.MarkUnknownAsync(
                new MigrationUnknownCommand(migration.MigrationId, safeErrorCode, null),
                CancellationToken.None);
        }
        catch (Exception)
        {
            phaseLogger.LogFailure("MigrationJournal", "MigrationUnknownStatePersistenceFailed");
        }
    }

    private async Task TryPersistFailureStateAsync(Guid integrationId, string safeErrorCode)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(apiOptions.Value.CompletionFailureStateTimeoutSeconds));
        try
        {
            var current = await repository.GetCurrentAsync(timeout.Token);
            if (current.HasCompleteConnection || current.IsMigrationBlocking)
            {
                return;
            }

            await repository.SetConnectionStateAsync(
                integrationId,
                safeErrorCode == "CompletionStateUnknown"
                    ? ConnectionState.Unknown
                    : ConnectionState.Error,
                safeErrorCode,
                timeout.Token);
        }
        catch (Exception)
        {
            phaseLogger.LogFailure("IntegrationPersistence", "CompletionStatePersistenceFailed");
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var integration = await repository.GetCurrentAsync(cancellationToken);
        if (integration.IsMigrationBlocking)
        {
            throw new BusinessValidationException(
                "The WhatsApp integration cannot be disconnected during a migration.");
        }

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
