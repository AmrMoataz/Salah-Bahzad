using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>Issues short-lived platform JWTs after Firebase identity verification (FR-PLAT-AUTH-002).</summary>
public interface IJwtTokenService
{
    PlatformToken IssueAccessToken(Staff staff);
    PlatformToken IssueRefreshToken(Staff staff);
    TokenPrincipal? ValidateRefreshToken(string refreshToken);
}

public sealed record PlatformToken(string Value, DateTimeOffset ExpiresAt);

public sealed record TokenPrincipal(Guid StaffId, Guid TenantId, string Role);
