using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class WhatsAppMigrationService(
    IWhatsAppMigrationRepository repository,
    IWhatsAppIntegrationRepository integrationRepository,
    ITenantContext tenantContext,
    IOptions<MetaOptions> metaOptions) : IWhatsAppMigrationService
{
    public async Task<WhatsAppMigrationStatus?> GetStatusAsync(CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var integration = await integrationRepository.GetCurrentAsync(cancellationToken);
        return await repository.GetStatusAsync(tenant.TenantId, integration.IntegrationId, cancellationToken);
    }

    public async Task<WhatsAppMigrationPreflight> GetPreflightAsync(
        CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var integration = await integrationRepository.GetCurrentAsync(cancellationToken);
        var preflight = await repository.GetPreflightAsync(
            tenant.TenantId, integration.IntegrationId, cancellationToken);
        var configurationAvailable = !string.IsNullOrWhiteSpace(
            metaOptions.Value.EffectiveCoexistenceEmbeddedSignupConfigId);

        if (configurationAvailable)
        {
            return preflight with { CoexistenceConfigurationAvailable = true };
        }

        return preflight with
        {
            CanBegin = false,
            BlockingCode = "CoexistenceConfigurationMissing",
            CoexistenceConfigurationAvailable = false
        };
    }

    public async Task<WhatsAppMigrationStatus> BeginAsync(
        BeginMigrationCommand command, CancellationToken cancellationToken)
    {
        var preflight = await GetPreflightAsync(cancellationToken);
        if (!preflight.CanBegin)
        {
            throw new BusinessValidationException(
                "The WhatsApp migration cannot begin while the integration is not ready.");
        }

        if (command.ApprovalReference?.Length > 256 || command.OperationalNotes?.Length > 2048)
        {
            throw new BusinessValidationException("The migration approval metadata is invalid.");
        }

        return await repository.BeginAsync(
            preflight.TenantId,
            preflight.IntegrationId,
            command.ApprovalReference,
            command.OperationalNotes,
            cancellationToken);
    }

    public async Task<WhatsAppMigrationStatus> CancelAsync(CancellationToken cancellationToken)
    {
        var status = await GetRequiredStatusAsync(cancellationToken);
        if (!status.ExternalMutationOccurred &&
            status.CompletionState == MigrationCompletionState.Pending &&
            status.TransitionState is not (TransitionState.None or TransitionState.Completed))
        {
            return await repository.CancelAsync(status.TenantId, status.IntegrationId, cancellationToken);
        }

        throw new BusinessValidationException(
            "The WhatsApp migration cannot be canceled after an external operation may have occurred.");
    }

    public async Task<WhatsAppMigrationStatus> MarkExternalMutationAsync(
        MigrationExternalMutationCommand command,
        CancellationToken cancellationToken)
    {
        var status = await GetRequiredStatusAsync(cancellationToken);
        ValidateCurrentMigration(status, command.MigrationId);
        if (command.NextState is not (
                TransitionState.AwaitingBusinessApp or
                TransitionState.AwaitingCoexistenceSignup or
                TransitionState.Reconciling))
        {
            throw new BusinessValidationException("The external migration phase is invalid.");
        }

        return await repository.MarkExternalMutationAsync(
            status.TenantId, status.IntegrationId, command, cancellationToken);
    }

    public async Task<WhatsAppMigrationStatus> MarkUnknownAsync(
        MigrationUnknownCommand command,
        CancellationToken cancellationToken)
    {
        var status = await GetRequiredStatusAsync(cancellationToken);
        ValidateCurrentMigration(status, command.MigrationId);
        if (string.IsNullOrWhiteSpace(command.SafeErrorCode) || command.SafeErrorCode.Length > 100)
        {
            throw new BusinessValidationException("The migration error code is invalid.");
        }

        return await repository.MarkUnknownAsync(
            status.TenantId, status.IntegrationId, command, cancellationToken);
    }

    public async Task<WhatsAppIntegration> ReconcileSignupAsync(
        MetaSignupResult result, CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        if (result.TenantId != tenant.TenantId || result.OnboardingType != WhatsAppOnboardingType.Coexistence)
        {
            throw new BusinessValidationException("Only a Tenant-scoped Coexistence result can be reconciled.");
        }

        var status = await repository.GetStatusAsync(
            tenant.TenantId, result.IntegrationId, cancellationToken);
        if (status is null || !status.ExternalMutationOccurred ||
            status.CompletionState is MigrationCompletionState.Succeeded or MigrationCompletionState.Canceled)
        {
            throw new BusinessValidationException("The Coexistence migration is not awaiting reconciliation.");
        }

        if (string.IsNullOrWhiteSpace(result.WabaId) ||
            string.IsNullOrWhiteSpace(result.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(result.AccessTokenSecretRef) ||
            string.IsNullOrWhiteSpace(result.EffectiveEmbeddedSignupConfigId))
        {
            throw new BusinessValidationException("The Coexistence result is incomplete.");
        }

        return await repository.ReconcileSignupAsync(result, cancellationToken);
    }

    private async Task<WhatsAppMigrationStatus> GetRequiredStatusAsync(
        CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);
        return status ?? throw new BusinessValidationException("No WhatsApp migration is active.");
    }

    private static void ValidateCurrentMigration(
        WhatsAppMigrationStatus status, Guid migrationId)
    {
        if (status.MigrationId != migrationId ||
            status.TransitionState is TransitionState.None or TransitionState.Completed ||
            status.CompletionState is MigrationCompletionState.Succeeded or MigrationCompletionState.Canceled)
        {
            throw new BusinessValidationException("The WhatsApp migration is not active.");
        }
    }
}
