using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.DTOs;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Validates a platform refresh token (signature, issuer, audience, lifetime and <c>token_type=refresh</c>)
/// and reissues a fresh access+refresh pair. <b>Role-aware</b> (S0): a <c>role=Student</c> token reloads the
/// <c>Student</c>, re-checks it is <c>Active</c> and that its bound device is still active, and reissues a
/// student pair preserving <c>device_id</c>; any other role takes the staff path. Reloading from the
/// database (rather than trusting the token) means an account deactivated/deleted — or a device cleared by
/// staff — since the token was issued can no longer refresh. Any failure → 401, so the client falls back to
/// a full sign-in (FR-PLAT-AUTH-002, FR-ADM-AUTH-001, FR-PLAT-DEV-003/004).
/// </summary>
internal sealed class RefreshTokenHandler(
    IJwtTokenService jwtTokenService,
    IAppDbContext db,
    ILogger<RefreshTokenHandler> logger)
    : IRequestHandler<RefreshTokenCommand, RefreshResult>
{
    public async ValueTask<RefreshResult> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var principal = jwtTokenService.ValidateRefreshToken(command.RefreshToken);
        if (principal is null)
        {
            logger.LogWarning("Refresh rejected — token failed validation");
            throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
        }

        return string.Equals(principal.Role, "Student", StringComparison.Ordinal)
            ? await RefreshStudentAsync(principal, cancellationToken)
            : await RefreshStaffAsync(principal, cancellationToken);
    }

    private async Task<RefreshResult> RefreshStaffAsync(TokenPrincipal principal, CancellationToken cancellationToken)
    {
        // Refresh runs without a platform JWT (the access token is expired) → no tenant claim, so the global
        // filter would hide every row; IgnoreQueryFilters bypasses the tenant + soft-delete filters and
        // IsDeleted/IsActive are re-checked explicitly.
        var staff = await db.Staff
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == principal.UserId, cancellationToken);

        if (staff is null || !staff.IsActive || staff.IsDeleted)
        {
            logger.LogWarning(
                "Refresh rejected — staff {StaffId} is missing, inactive, or deleted", principal.UserId);
            throw new UnauthorizedAccessException("Your account is no longer active. Please sign in again.");
        }

        var accessToken = jwtTokenService.IssueAccessToken(staff);
        var refreshToken = jwtTokenService.IssueRefreshToken(staff);

        logger.LogInformation("Staff {StaffId} ({Role}) refreshed their session", staff.Id, staff.Role);

        var permissions = PermissionCatalog.ForRole(staff.Role);

        return new RefreshResult(
            Staff: new AuthTokenResponse(
                accessToken.Value,
                refreshToken.Value,
                accessToken.ExpiresAt,
                refreshToken.ExpiresAt,
                new StaffInfo(staff.Id, staff.DisplayName, staff.Email, staff.Role, permissions)),
            Student: null);
    }

    private async Task<RefreshResult> RefreshStudentAsync(
        TokenPrincipal principal, CancellationToken cancellationToken)
    {
        var student = await db.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == principal.UserId, cancellationToken);

        if (student is null || student.IsDeleted || student.Status != StudentStatus.Active)
        {
            logger.LogWarning(
                "Refresh rejected — student {StudentId} is missing, inactive, or deleted", principal.UserId);
            throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
        }

        // The device identity travels in the signed token; re-verify it still maps to an active binding so a
        // staff-cleared device (FR-PLAT-DEV-004) cannot keep refreshing — it must sign in again to re-bind.
        if (!Guid.TryParse(principal.DeviceId, out var deviceId))
        {
            logger.LogWarning("Refresh rejected — student {StudentId} token carries no device_id", student.Id);
            throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
        }

        var device = await db.StudentDevices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                d => d.Id == deviceId && d.StudentId == student.Id && d.IsActive, cancellationToken);

        if (device is null)
        {
            logger.LogWarning(
                "Refresh rejected — student {StudentId} device {DeviceId} is not an active binding",
                student.Id, deviceId);
            throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
        }

        var accessToken = jwtTokenService.IssueStudentAccessToken(student, device.Id);
        var refreshToken = jwtTokenService.IssueStudentRefreshToken(student, device.Id);

        logger.LogInformation("Student {StudentId} refreshed their session (device {DeviceId})", student.Id, device.Id);

        return new RefreshResult(
            Staff: null,
            Student: new StudentAuthResponse(
                accessToken.Value,
                refreshToken.Value,
                accessToken.ExpiresAt,
                refreshToken.ExpiresAt,
                new StudentInfo(
                    student.Id,
                    student.FullName,
                    student.Status,
                    new BoundDeviceInfo(device.FingerprintSummary, device.BoundAtUtc))));
    }
}
