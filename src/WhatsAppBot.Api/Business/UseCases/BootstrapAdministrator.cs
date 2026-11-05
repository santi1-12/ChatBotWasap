using Microsoft.AspNetCore.Identity;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Entities;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class BootstrapAdministrator(
    IAdministratorRepository repository,
    IPasswordHasher<Administrator> passwordHasher) : IAdministratorBootstrapper
{
    public async Task<bool> CreateFirstAdministratorAsync(
        string email,
        string password,
        string tenantLegalName,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var normalizedTenantLegalName = tenantLegalName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTenantLegalName) || normalizedTenantLegalName.Length > 256)
        {
            throw new ArgumentException("The bootstrap Tenant legal name must contain 1 to 256 characters.",
                nameof(tenantLegalName));
        }

        var administrator = new Administrator(0, normalizedEmail, string.Empty, true);
        var passwordHash = passwordHasher.HashPassword(administrator, password);
        return await repository.CreateFirstAdministratorAsync(
            normalizedEmail, passwordHash, normalizedTenantLegalName, cancellationToken);
    }
}
