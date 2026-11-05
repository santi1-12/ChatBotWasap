using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Security;

namespace WhatsAppBot.Api.ContractTests;

public sealed class SecurityAndFailureTests
{
    [Fact]
    public async Task InvalidTenantClaimFailsClosed()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "19"),
                    new Claim(TenantClaimTypes.TenantId, "not-a-guid")
                ],
                "ContractTest"))
        };
        var context = new TenantContext(
            new HttpContextAccessor { HttpContext = httpContext },
            new NullTenantRepository());

        await Assert.ThrowsAsync<TenantContextUnavailableException>(
            () => context.GetRequiredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnauthenticatedRequestsCannotResolveTenantContext()
    {
        var context = new TenantContext(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new NullTenantRepository());

        await Assert.ThrowsAsync<TenantContextUnavailableException>(
            () => context.GetRequiredAsync(CancellationToken.None));
    }

    private sealed class NullTenantRepository : ITenantRepository
    {
        public Task<TenantScope?> GetActiveForAdministratorAsync(
            long administratorId,
            Guid? tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantScope?>(null);
    }
}
