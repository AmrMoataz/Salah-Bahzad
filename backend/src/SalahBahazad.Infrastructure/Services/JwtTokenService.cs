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

    public PlatformToken IssueAccessToken(Staff staff) =>
        BuildToken(staff.Id, staff.TenantId, staff.Role.ToString(), deviceId: null, AccessExpiry(), "access");

    public PlatformToken IssueRefreshToken(Staff staff) =>
        BuildToken(staff.Id, staff.TenantId, staff.Role.ToString(), deviceId: null, RefreshExpiry(), "refresh");

    public PlatformToken IssueStudentAccessToken(Student student, Guid? deviceId) =>
        BuildToken(student.Id, student.TenantId, "Student", deviceId, AccessExpiry(), "access");

    public PlatformToken IssueStudentRefreshToken(Student student, Guid? deviceId) =>
        BuildToken(student.Id, student.TenantId, "Student", deviceId, RefreshExpiry(), "refresh");

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

            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tenantId = Guid.Parse(principal.FindFirstValue("tenant_id")!);
            var role = principal.FindFirstValue(ClaimTypes.Role)!;
            var deviceId = principal.FindFirstValue("device_id"); // null for staff tokens

            return new TokenPrincipal(userId, tenantId, role, deviceId);
        }
        catch
        {
            return null;
        }
    }

    private DateTimeOffset AccessExpiry() =>
        clock.GetUtcNow().AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 15));

    private DateTimeOffset RefreshExpiry() =>
        clock.GetUtcNow().AddDays(configuration.GetValue("Jwt:RefreshTokenDays", 7));

    private PlatformToken BuildToken(
        Guid userId, Guid tenantId, string role, Guid? deviceId, DateTimeOffset expiry, string tokenType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role),
            new("token_type", tokenType),
        };

        // Student tokens carry the bound device so CurrentUserResolver can attribute audit and the
        // refresh handler can re-verify the binding (FR-PLAT-DEV-001/003).
        if (deviceId is { } id)
            claims.Add(new Claim("device_id", id.ToString()));

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
