using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Auth.DTOs;

/// <summary>Returned to the client after a successful Firebase token exchange (FR-PLAT-AUTH-002).</summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    StaffInfo Staff);

public sealed record StaffInfo(
    Guid Id,
    string DisplayName,
    string Email,
    StaffRole Role,
    IReadOnlyList<Permission> Permissions);
