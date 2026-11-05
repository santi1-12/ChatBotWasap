using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Responses;

namespace WhatsAppBot.Api.Infrastructure.Security;

public sealed class AccessTokenIssuer(IOptions<AuthenticationOptions> options) : IAccessTokenIssuer
{
    public IssuedAccessToken Create(long administratorId, string normalizedEmail, Guid? activeTenantId = null)
    {
        var settings = options.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(settings.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, administratorId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, normalizedEmail),
            new Claim(ClaimTypes.NameIdentifier, administratorId.ToString()),
            new Claim(ClaimTypes.Name, normalizedEmail),
            new Claim(ClaimTypes.Role, "Administrator")
        };
        if (activeTenantId.HasValue)
        {
            claims.Add(new Claim(TenantClaimTypes.TenantId, activeTenantId.Value.ToString("D")));
        }
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
