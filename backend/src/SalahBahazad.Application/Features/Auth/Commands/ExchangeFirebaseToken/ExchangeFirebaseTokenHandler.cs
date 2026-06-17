using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeFirebaseToken;

/// <summary>
/// Verifies the Firebase ID token, looks up the staff account, enforces that it is
/// an active staff member (non-staff Firebase accounts are rejected — FR-ADM-AUTH-001),
/// and issues a platform JWT pair.
/// </summary>
public sealed class ExchangeFirebaseTokenHandler(
    IFirebaseAuthService firebaseAuth,
    IJwtTokenService jwtTokenService,
    IAppDbContext db,
    ILogger<ExchangeFirebaseTokenHandler> logger)
    : IRequestHandler<ExchangeFirebaseTokenCommand, AuthTokenResponse>
{
    public async ValueTask<AuthTokenResponse> Handle(
        ExchangeFirebaseTokenCommand command,
        CancellationToken cancellationToken)
    {
        var claims = await firebaseAuth.VerifyIdTokenAsync(
            command.FirebaseIdToken, cancellationToken);

        // Look up the staff member by Firebase UID.
        // Login is a cross-tenant operation: the caller is not yet authenticated, so there is
        // no tenant claim and the global TenantId query filter would resolve to Guid.Empty and
        // hide every row. The Firebase UID identifies the account; we discover the tenant FROM
        // the resulting record (and stamp it into the JWT). IgnoreQueryFilters() also drops the
        // soft-delete filter, but IsDeleted is re-checked explicitly below.
        // Non-staff Firebase accounts won't exist here → 401 (FR-ADM-AUTH-001).
        var staff = await db.Staff
            .IgnoreQueryFilters()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.FirebaseUid == claims.Uid, cancellationToken);

        if (staff is null)
        {
            logger.LogWarning("Firebase UID {Uid} has no staff account — login rejected", claims.Uid);
            throw new UnauthorizedAccessException(
                "This account does not have staff access to the admin portal.");
        }

        if (!staff.IsActive || staff.IsDeleted)
        {
            logger.LogWarning("Staff {StaffId} is inactive or deleted — login rejected", staff.Id);
            throw new UnauthorizedAccessException("Your account has been deactivated.");
        }

        var accessToken = jwtTokenService.IssueAccessToken(staff);
        var refreshToken = jwtTokenService.IssueRefreshToken(staff);

        logger.LogInformation(
            "Staff {StaffId} ({Role}) signed in", staff.Id, staff.Role);

        var permissions = PermissionCatalog.ForRole(staff.Role);

        return new AuthTokenResponse(
            accessToken.Value,
            refreshToken.Value,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt,
            new StaffInfo(staff.Id, staff.DisplayName, staff.Email, staff.Role, permissions));
    }
}
