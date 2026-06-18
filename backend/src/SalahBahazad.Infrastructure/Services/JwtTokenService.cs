using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Issues short-lived platform JWTs containing userId / tenantId / role / deviceId.
/// Refresh tokens are longer-lived JWTs (server validates signature only; revocation
/// via Redis is added in the revocation feature). No passwords stored (FR-PLAT-AUTH-002/004).
/// </summary>
internal sealed class JwtTokenService(IConfiguration configuration, TimeProvider clock) : IJwtTokenService
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    public PlatformToken IssueAccessToken(Staff staff)
    {
        var expiry = clock.GetUtcNow().AddMinutes(
            configuration.GetValue("Jwt:AccessTokenMinutes", 15));
        return BuildToken(staff, expiry, "access");
    }

    public PlatformToken IssueRefreshToken(Staff staff)
    {
        var expiry = clock.GetUtcNow().AddDays(
            configuration.GetValue("Jwt:RefreshTokenDays", 7));
        return BuildToken(staff, expiry, "refresh");
    }

    public TokenPrincipal? ValidateRefreshToken(string refreshToken)
    {
        var key = GetSigningKey();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var principal = Handler.ValidateToken(refreshToken, parameters, out _);
            var tokenType = principal.FindFirstValue("token_type");
            if (tokenType != "refresh") return null;

            var staffId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tenantId = Guid.Parse(principal.FindFirstValue("tenant_id")!);
            var role = principal.FindFirstValue(ClaimTypes.Role)!;

            return new TokenPrincipal(staffId, tenantId, role);
        }
        catch
        {
            return null;
        }
    }

    private PlatformToken BuildToken(Staff staff, DateTimeOffset expiry, string tokenType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new("tenant_id", staff.TenantId.ToString()),
            new(ClaimTypes.Role, staff.Role.ToString()),
            new("token_type", tokenType),
        };

        var key = GetSigningKey();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiry.UtcDateTime,
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = Handler.CreateToken(descriptor);
        return new PlatformToken(Handler.WriteToken(token), expiry);
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}
