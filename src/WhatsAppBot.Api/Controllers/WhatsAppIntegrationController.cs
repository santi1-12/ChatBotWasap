using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;
using WhatsAppBot.Shared.Responses;
using WhatsAppBot.Api.Models.Entities;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/whatsapp")]
public sealed class WhatsAppIntegrationController(
    IWhatsAppIntegrationService integrationService,
    IWhatsAppMigrationService migrationService,
    IBotConfigurationService botConfigurationService,
    IOptions<MetaOptions> metaOptions) : ControllerBase
{
    [HttpGet("integration")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            await GetDtoAsync(cancellationToken), HttpContext.TraceIdentifier));
    }

    [HttpGet("migration/preflight")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppMigrationPreflightDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<WhatsAppMigrationPreflightDTO>>> GetMigrationPreflightAsync(
        CancellationToken cancellationToken)
    {
        var preflight = await migrationService.GetPreflightAsync(cancellationToken);
        return Ok(ResponseE<WhatsAppMigrationPreflightDTO>.Ok(
            new WhatsAppMigrationPreflightDTO
            {
                TenantId = preflight.TenantId,
                IntegrationId = preflight.IntegrationId,
                CanBegin = preflight.CanBegin,
                BlockingCode = preflight.BlockingCode,
                ConnectionState = preflight.ConnectionState,
                OnboardingType = preflight.OnboardingType,
                TransitionState = preflight.TransitionState,
                HasMetaIdentity = preflight.HasMetaIdentity,
                CoexistenceConfigurationAvailable = preflight.CoexistenceConfigurationAvailable,
                SendingOutboxCount = preflight.SendingOutboxCount,
                PendingOutboxCount = preflight.PendingOutboxCount,
                HeldOutboxCount = preflight.HeldOutboxCount,
                ProcessingWebhookCount = preflight.ProcessingWebhookCount
            },
            HttpContext.TraceIdentifier));
    }

    [HttpPost("migration/begin")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> BeginMigrationAsync(
        [FromBody] BeginWhatsAppMigrationRequestDTO request,
        CancellationToken cancellationToken)
    {
        await migrationService.BeginAsync(
            new BeginMigrationCommand(request.ApprovalReference, request.OperationalNotes),
            cancellationToken);
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            await GetDtoAsync(cancellationToken), HttpContext.TraceIdentifier));
    }

    [HttpPost("migration/cancel")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> CancelMigrationAsync(
        CancellationToken cancellationToken)
    {
        await migrationService.CancelAsync(cancellationToken);
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            await GetDtoAsync(cancellationToken), HttpContext.TraceIdentifier));
    }

    [HttpPost("migration/external-mutation")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> MarkMigrationExternalMutationAsync(
        [FromBody] WhatsAppMigrationExternalMutationRequestDTO request,
        CancellationToken cancellationToken)
    {
        await migrationService.MarkExternalMutationAsync(
            new MigrationExternalMutationCommand(
                request.MigrationId,
                request.NextState,
                request.ExternalOperationReferenceId),
            cancellationToken);
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            await GetDtoAsync(cancellationToken), HttpContext.TraceIdentifier));
    }

    [HttpPost("migration/unknown")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> MarkMigrationUnknownAsync(
        [FromBody] WhatsAppMigrationUnknownRequestDTO request,
        CancellationToken cancellationToken)
    {
        await migrationService.MarkUnknownAsync(
            new MigrationUnknownCommand(
                request.MigrationId,
                request.SafeErrorCode,
                request.ExternalOperationReferenceId),
            cancellationToken);
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            await GetDtoAsync(cancellationToken), HttpContext.TraceIdentifier));
    }

    [HttpPost("embedded-signup")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> CompleteSignupAsync(
        [FromBody] EmbeddedSignupCompletionRequestDTO request,
        CancellationToken cancellationToken)
    {
        await integrationService.CompleteSignupAsync(
            request.AuthorizationCode,
            request.WabaId,
            string.IsNullOrWhiteSpace(request.PhoneNumberId) ? null : request.PhoneNumberId,
            request.OnboardingType!.Value,
            cancellationToken,
            request.ImportHistory);
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            await GetDtoAsync(cancellationToken), HttpContext.TraceIdentifier));
    }

    [HttpDelete("integration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisconnectAsync(CancellationToken cancellationToken)
    {
        await integrationService.DisconnectAsync(cancellationToken);
        return NoContent();
    }

    private async Task<WhatsAppIntegrationDTO> GetDtoAsync(CancellationToken cancellationToken)
    {
        var integration = await integrationService.GetCurrentAsync(cancellationToken);
        var configuration = await botConfigurationService.GetAsync(cancellationToken);
        var history = await integrationService.GetHistoryImportStatusAsync(cancellationToken);
        var migration = await migrationService.GetStatusAsync(cancellationToken);
        return Map(integration, configuration.IsBotEnabled, history, migration);
    }

    private WhatsAppIntegrationDTO Map(
        WhatsAppIntegration integration,
        bool botEnabled,
        HistoryImportStatusRecord? history,
        WhatsAppMigrationStatus? migration) =>
        CreateDto(integration, botEnabled, history, migration);

    private WhatsAppIntegrationDTO CreateDto(
        WhatsAppIntegration integration,
        bool botEnabled,
        HistoryImportStatusRecord? history,
        WhatsAppMigrationStatus? migration)
    {
        var completionStateUnknown = integration.ConnectionState == ConnectionState.Unknown ||
                                     string.Equals(
                                         integration.LastConnectionErrorCode,
                                         "CompletionStateUnknown",
                                         StringComparison.Ordinal);
        var connectionState = completionStateUnknown ||
                              (integration.ConnectionState == ConnectionState.Connected &&
                               !integration.HasCompleteConnection)
            ? ConnectionState.Unknown
            : integration.ConnectionState;
        return new()
        {
            TenantId = integration.TenantId,
            WabaId = integration.WabaId,
            PhoneNumberId = integration.PhoneNumberId,
            BusinessPhoneNumber = integration.BusinessPhoneNumber,
            DisplayName = integration.DisplayName,
            OnboardingType = integration.OnboardingType,
            ConnectionState = connectionState,
            TransitionState = integration.TransitionState,
            TransitionStartedAtUtc = integration.TransitionStartedAtUtc,
            MigrationId = migration?.MigrationId,
            MigrationCompletionState = migration?.CompletionState ?? MigrationCompletionState.None,
            MigrationExternalMutationOccurred = migration?.ExternalMutationOccurred ?? false,
            MigrationExpectedExternalOperation = migration?.ExpectedExternalOperation,
            MigrationExternalOperationReferenceId = migration?.ExternalOperationReferenceId,
            MigrationErrorCode = migration?.ErrorCode,
            MigrationStartedAtUtc = migration?.StartedAtUtc,
            MigrationUpdatedAtUtc = migration?.UpdatedAtUtc,
            MigrationHeldOutboxCount = migration?.HeldOutboxCount ?? 0,
            MigrationSendingOutboxCount = migration?.SendingOutboxCount ?? 0,
            ConnectionErrorCode = connectionState == ConnectionState.Unknown &&
                                  string.IsNullOrWhiteSpace(integration.LastConnectionErrorCode)
                ? "CompletionStateUnknown"
                : integration.LastConnectionErrorCode,
            BotEnabled = botEnabled,
            ConnectedAtUtc = integration.ConnectedAtUtc,
            HasCredentialReference = !string.IsNullOrWhiteSpace(integration.AccessTokenSecretRef),
            MetaAppId = metaOptions.Value.AppId,
            EmbeddedSignupConfigId = metaOptions.Value.EmbeddedSignupConfigId,
            CoexistenceEmbeddedSignupConfigId = metaOptions.Value.EffectiveCoexistenceEmbeddedSignupConfigId,
            EffectiveEmbeddedSignupConfigId = integration.EffectiveEmbeddedSignupConfigId,
            MetaGraphApiVersion = metaOptions.Value.GraphApiVersion,
            HistoryImportRequested = integration.HistoryImportRequested,
            HistoryImportState = history?.State ?? (integration.HistoryImportRequested
                ? HistoryImportState.Pending
                : HistoryImportState.NotRequested),
            HistoryTotalCount = history?.TotalCount ?? 0,
            HistoryProcessedCount = history?.ProcessedCount ?? 0,
            HistoryImportedCount = history?.ImportedCount ?? 0,
            HistoryDuplicateCount = history?.DuplicateCount ?? 0,
            HistorySkippedCount = history?.SkippedCount ?? 0,
            HistoryFailedCount = history?.FailedCount ?? 0,
            HistoryStartedAtUtc = history?.StartedAtUtc,
            HistoryCompletedAtUtc = history?.CompletedAtUtc,
            HistoryErrorCode = history?.ErrorCode
        };
    }
}
