using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.Abstractions;

public interface ITenantRepository
{
    Task<TenantScope?> GetActiveForAdministratorAsync(
        long administratorId,
        Guid? tenantId,
        CancellationToken cancellationToken);
}

public interface ITenantContext
{
    TenantScope? Current { get; }

    Task<TenantScope> GetRequiredAsync(CancellationToken cancellationToken);
}

public sealed record TenantScope(
    TenantRecord Tenant,
    TenantMembershipRecord Membership)
{
    public Guid TenantId => Tenant.TenantId;
    public long AdministratorId => Membership.AdminId;
}

public sealed class TenantContextUnavailableException(string message) : Exception(message);
