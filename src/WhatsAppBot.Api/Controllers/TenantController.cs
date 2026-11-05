using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/tenant")]
public sealed class TenantController(ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ResponseE<TenantDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<TenantDTO>>> GetCurrent(CancellationToken cancellationToken)
    {
        var scope = await tenantContext.GetRequiredAsync(cancellationToken);
        var tenant = scope.Tenant;
        return Ok(ResponseE<TenantDTO>.Ok(
            new TenantDTO
            {
                TenantId = tenant.TenantId,
                LegalName = tenant.LegalName,
                IsActive = tenant.IsActive,
                CreatedAtUtc = tenant.CreatedAtUtc,
                UpdatedAtUtc = tenant.UpdatedAtUtc
            },
            HttpContext.TraceIdentifier));
    }
}
