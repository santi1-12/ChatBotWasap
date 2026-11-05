using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Security;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.ContractTests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantContextDoesNotFallBackToAnUnspecifiedTenant()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "19")],
                "ContractTest"))
        };
        var repository = new MembershipRepository();
        var context = new TenantContext(new HttpContextAccessor { HttpContext = httpContext }, repository);

        await Assert.ThrowsAsync<TenantContextUnavailableException>(
            () => context.GetRequiredAsync(CancellationToken.None));
        Assert.Null(repository.RequestedTenantId);
    }

    [Fact]
    public async Task TenantContextRejectsAClaimForAnInactiveOrForeignMembership()
    {
        var claimedTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "19"),
                    new Claim(TenantClaimTypes.TenantId, claimedTenantId.ToString("D"))
                ],
                "ContractTest"))
        };
        var repository = new MembershipRepository();
        var context = new TenantContext(new HttpContextAccessor { HttpContext = httpContext }, repository);

        await Assert.ThrowsAsync<TenantContextUnavailableException>(
            () => context.GetRequiredAsync(CancellationToken.None));
        Assert.Equal(claimedTenantId, repository.RequestedTenantId);
    }

    private sealed class MembershipRepository : ITenantRepository
    {
        public Guid? RequestedTenantId { get; private set; }

        public Task<TenantScope?> GetActiveForAdministratorAsync(
            long administratorId,
            Guid? tenantId,
            CancellationToken cancellationToken)
        {
            RequestedTenantId = tenantId;
            return Task.FromResult<TenantScope?>(null);
        }
    }
}
