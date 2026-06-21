using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>Issues short-lived platform JWTs after Firebase identity verification (FR-PLAT-AUTH-002).</summary>
public interface IJwtTokenService
{
    PlatformToken IssueAccessToken(Staff staff);
    PlatformToken IssueRefreshToken(Staff staff);

    /// <summary>
    /// Issues a student access token: <c>role=Student</c>, the student's tenant, and the bound
    /// <paramref name="deviceId"/> stamped as the <c>device_id</c> claim that the resolvers read
    /// (FR-PLAT-AUTH-002, FR-PLAT-DEV-001).
    /// </summary>
    PlatformToken IssueStudentAccessToken(Student student, Guid deviceId);

    /// <summary>Issues the matching student refresh token, preserving the <paramref name="deviceId"/>.</summary>
    PlatformToken IssueStudentRefreshToken(Student student, Guid deviceId);

    TokenPrincipal? ValidateRefreshToken(string refreshToken);
}

public sealed record PlatformToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// The validated identity carried by a refresh token. <see cref="Role"/> lets the refresh handler branch
/// staff vs. student; <see cref="DeviceId"/> is present only for student tokens so the reissued pair can
/// preserve the device binding (FR-PLAT-AUTH-002, FR-PLAT-DEV-003).
/// </summary>
public sealed record TokenPrincipal(Guid UserId, Guid TenantId, string Role, string? DeviceId = null);
