using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Auth.DTOs;

/// <summary>
/// Returned to the student client after a successful Firebase token exchange (#1) or a role-aware
/// refresh whose token is a student's (#2). Parallels <see cref="AuthTokenResponse"/> but carries the
/// student identity instead of staff. Kept minimal — email/avatar are S6's <c>/api/me/profile</c>
/// concern; <see cref="StudentInfo.FullName"/> feeds the portal's "Welcome back, {firstName}!"
/// (FR-STU-AUTH-001, FR-PLAT-AUTH-002).
/// </summary>
public sealed record StudentAuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    StudentInfo Student);

public sealed record StudentInfo(
    Guid Id,
    string FullName,
    StudentStatus Status,
    BoundDeviceInfo? BoundDevice);

/// <summary>The student's single bound device, surfaced so the portal can show / offer to reset it (FR-PLAT-DEV-006).</summary>
public sealed record BoundDeviceInfo(
    string? Summary,
    DateTimeOffset BoundAtUtc);
