using Microsoft.AspNetCore.Identity;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Api.Models.Requests;
using WhatsAppBot.Api.Models.Responses;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class AuthenticateAdministrator(
    IAdministratorRepository repository,
    ITenantRepository tenantRepository,
    IAccessTokenIssuer tokenIssuer,
    IPasswordHasher<Administrator> passwordHasher) : IAdministratorAuthenticator
{
    public async Task<IssuedAccessToken?> AuthenticateAsync(
        AdministratorCredentials credentials, CancellationToken cancellationToken)
    {
        var normalizedEmail = credentials.Email.Trim().ToUpperInvariant();
        var administrator = await repository.GetForAuthenticationAsync(normalizedEmail, cancellationToken);
        if (administrator is null || !administrator.IsActive)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            administrator, administrator.PasswordHash, credentials.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        await repository.UpdateLastLoginAsync(administrator.AdminId, cancellationToken);
        var activeTenant = await tenantRepository.GetActiveForAdministratorAsync(
            administrator.AdminId, null, cancellationToken);
        return tokenIssuer.Create(administrator.AdminId, normalizedEmail, activeTenant?.TenantId);
    }
}
