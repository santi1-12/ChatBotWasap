using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Infrastructure.Security;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;

namespace WhatsAppBot.Business.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public async Task ContextResolvesTheServerIssuedTenantClaimThroughMembership()
    {
        var tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var repository = new FakeTenantRepository(CreateScope(tenantId, 19));
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "19"),
                    new Claim(TenantClaimTypes.TenantId, tenantId.ToString("D"))
                ],
                "Bearer"))
        };
        var context = new TenantContext(new HttpContextAccessor { HttpContext = httpContext }, repository);

        var resolved = await context.GetRequiredAsync(CancellationToken.None);

        Assert.Equal(tenantId, resolved.TenantId);
        Assert.Equal(19, resolved.AdministratorId);
        Assert.Equal(tenantId, repository.RequestedTenantId);
        Assert.Same(resolved, context.Current);
    }

    [Fact]
    public async Task ContextRejectsMissingAdministratorIdentity()
    {
        var context = new TenantContext(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new FakeTenantRepository(null));

        await Assert.ThrowsAsync<TenantContextUnavailableException>(
            () => context.GetRequiredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task BootstrapForwardsTheTenantNameToTheAtomicRepositoryOperation()
    {
        var repository = new FakeAdministratorRepository();
        var bootstrapper = new BootstrapAdministrator(
            repository,
            new PasswordHasher<Administrator>());

        var created = await bootstrapper.CreateFirstAdministratorAsync(
            " admin@example.test ", "a-password-long-enough", " Acme ", CancellationToken.None);

        Assert.True(created);
        Assert.Equal("ADMIN@EXAMPLE.TEST", repository.Email);
        Assert.Equal("Acme", repository.TenantLegalName);
        Assert.False(string.IsNullOrWhiteSpace(repository.PasswordHash));
    }

    private static TenantScope CreateScope(Guid tenantId, long adminId) =>
        new(
            new TenantRecord(
                tenantId,
                "Test Tenant",
                true,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            new TenantMembershipRecord(
                1,
                tenantId,
                adminId,
                true,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));

    private sealed class FakeTenantRepository(TenantScope? scope) : ITenantRepository
    {
        public Guid? RequestedTenantId { get; private set; }

        public Task<TenantScope?> GetActiveForAdministratorAsync(
            long administratorId,
            Guid? tenantId,
            CancellationToken cancellationToken)
        {
            RequestedTenantId = tenantId;
            return Task.FromResult(scope);
        }
    }

    private sealed class FakeAdministratorRepository : IAdministratorRepository
    {
        public string? Email { get; private set; }
        public string? PasswordHash { get; private set; }
        public string? TenantLegalName { get; private set; }

        public Task<Administrator?> GetForAuthenticationAsync(
            string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<Administrator?>(null);

        public Task UpdateLastLoginAsync(long adminId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> CreateFirstAdministratorAsync(
            string normalizedEmail,
            string passwordHash,
            string tenantLegalName,
            CancellationToken cancellationToken)
        {
            Email = normalizedEmail;
            PasswordHash = passwordHash;
            TenantLegalName = tenantLegalName;
            return Task.FromResult(true);
        }
    }
}
