using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Infrastructure.Security;

public static class TenantClaimTypes
{
    public const string TenantId = "tenant_id";
}

public sealed class TenantContext(
    IHttpContextAccessor httpContextAccessor,
    ITenantRepository tenantRepository) : ITenantContext
{
    public TenantScope? Current { get; private set; }

    public async Task<TenantScope> GetRequiredAsync(CancellationToken cancellationToken)
    {
        if (Current is not null)
        {
            return Current;
        }

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new TenantContextUnavailableException(
                "An authenticated administrator is required to resolve the active Tenant.");
        }

        var administratorId = ParseAdministratorId(user);
        var tenantId = ParseTenantId(user);
        Current = await tenantRepository.GetActiveForAdministratorAsync(
            administratorId, tenantId, cancellationToken)
            ?? throw new TenantContextUnavailableException(
                "The authenticated administrator has no active Tenant membership.");

        return Current;
    }

    private static long ParseAdministratorId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out var administratorId) && administratorId > 0
            ? administratorId
            : throw new TenantContextUnavailableException(
                "The authenticated administrator identity is invalid.");
    }

    private static Guid? ParseTenantId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(TenantClaimTypes.TenantId);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var tenantId)
            ? tenantId
            : throw new TenantContextUnavailableException("The active Tenant claim is invalid.");
    }
}
