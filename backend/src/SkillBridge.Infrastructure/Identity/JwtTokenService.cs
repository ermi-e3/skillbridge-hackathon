using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    public AccessToken Generate(AuthUser user)
    {
        var settings = options.Value;
        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddHours(settings.ExpirationHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
