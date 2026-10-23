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
    IBotConfigurationService botConfigurationService,
    IOptions<MetaOptions> metaOptions) : ControllerBase
{
    [HttpGet("integration")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        var integration = await integrationService.GetCurrentAsync(cancellationToken);
        var configuration = await botConfigurationService.GetAsync(cancellationToken);
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            Map(integration, configuration.IsBotEnabled), HttpContext.TraceIdentifier));
    }

    [HttpPost("embedded-signup")]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseE<WhatsAppIntegrationDTO>), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ResponseE<WhatsAppIntegrationDTO>>> CompleteSignupAsync(
        [FromBody] EmbeddedSignupCompletionRequestDTO request,
        CancellationToken cancellationToken)
    {
        var integration = await integrationService.CompleteSignupAsync(
            request.AuthorizationCode,
            request.WabaId,
            string.IsNullOrWhiteSpace(request.PhoneNumberId) ? null : request.PhoneNumberId,
            request.OnboardingType!.Value,
            cancellationToken);
        var configuration = await botConfigurationService.GetAsync(cancellationToken);
        return Ok(ResponseE<WhatsAppIntegrationDTO>.Ok(
            Map(integration, configuration.IsBotEnabled), HttpContext.TraceIdentifier));
    }

    [HttpDelete("integration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisconnectAsync(CancellationToken cancellationToken)
    {
        await integrationService.DisconnectAsync(cancellationToken);
        return NoContent();
    }

    private WhatsAppIntegrationDTO Map(WhatsAppIntegration integration, bool botEnabled) =>
        new()
        {
            TenantId = integration.TenantId,
            WabaId = integration.WabaId,
            PhoneNumberId = integration.PhoneNumberId,
            BusinessPhoneNumber = integration.BusinessPhoneNumber,
            DisplayName = integration.DisplayName,
            OnboardingType = integration.OnboardingType,
            ConnectionState = integration.ConnectionState == ConnectionState.Connected &&
                              !integration.HasCompleteConnection
                ? ConnectionState.Error
                : integration.ConnectionState,
            ConnectionErrorCode = integration.LastConnectionErrorCode,
            BotEnabled = botEnabled,
            ConnectedAtUtc = integration.ConnectedAtUtc,
            HasCredentialReference = !string.IsNullOrWhiteSpace(integration.AccessTokenSecretRef),
            MetaAppId = metaOptions.Value.AppId,
            EmbeddedSignupConfigId = metaOptions.Value.EmbeddedSignupConfigId,
            CoexistenceEmbeddedSignupConfigId = metaOptions.Value.EffectiveCoexistenceEmbeddedSignupConfigId,
            MetaGraphApiVersion = metaOptions.Value.GraphApiVersion
        };
}
