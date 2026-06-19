using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Validates a platform refresh token (signature, issuer, audience, lifetime and
/// <c>token_type=refresh</c>), re-loads the staff account it identifies, re-checks that the
/// account is still active, and issues a fresh access+refresh pair. Any failure — malformed,
/// expired, wrong type, or an account since deactivated/deleted — is surfaced as 401 so the
/// client falls back to a full sign-in (FR-PLAT-AUTH-002, FR-ADM-AUTH-001).
/// </summary>
internal sealed class RefreshTokenHandler(
    IJwtTokenService jwtTokenService,
    IAppDbContext db,
    ILogger<RefreshTokenHandler> logger)
    : IRequestHandler<RefreshTokenCommand, AuthTokenResponse>
{
    public async ValueTask<AuthTokenResponse> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var principal = jwtTokenService.ValidateRefreshToken(command.RefreshToken);
        if (principal is null)
        {
            logger.LogWarning("Refresh rejected — token failed validation");
            throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
        }

        // Re-load the staff member by the id embedded in the refresh token. Refresh runs without a
        // platform JWT (the access token is expired), so there is no tenant claim and the global
        // TenantId query filter would hide every row — IgnoreQueryFilters() (as in the exchange
        // handler) bypasses both the tenant and soft-delete filters; IsDeleted is re-checked below.
        // Reloading from the database (rather than trusting the token's role claim) means a staff
        // member deactivated or deleted since the token was issued can no longer refresh.
        var staff = await db.Staff
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == principal.StaffId, cancellationToken);

        if (staff is null || !staff.IsActive || staff.IsDeleted)
        {
            logger.LogWarning(
                "Refresh rejected — staff {StaffId} is missing, inactive, or deleted", principal.StaffId);
            throw new UnauthorizedAccessException("Your account is no longer active. Please sign in again.");
        }

        var accessToken = jwtTokenService.IssueAccessToken(staff);
        var refreshToken = jwtTokenService.IssueRefreshToken(staff);

        logger.LogInformation("Staff {StaffId} ({Role}) refreshed their session", staff.Id, staff.Role);

        var permissions = PermissionCatalog.ForRole(staff.Role);

        return new AuthTokenResponse(
            accessToken.Value,
            refreshToken.Value,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt,
            new StaffInfo(staff.Id, staff.DisplayName, staff.Email, staff.Role, permissions));
    }
}
