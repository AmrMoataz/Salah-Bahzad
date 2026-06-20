using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>Resolves the authenticated staff member from the platform JWT (FR-PLAT-AUTH-002).</summary>
internal sealed class CurrentUserResolver(
    IHttpContextAccessor httpContextAccessor, ISystemOperationContext systemOperation) : ICurrentUserResolver
{
    // A System operation (Hangfire job / hub callback) has no request principal — it is never an authenticated
    // user, so the audit row is attributed to System (FR-PLAT-AUD-005).
    public bool IsAuthenticated =>
        systemOperation.Current is null
        && httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public Guid TenantId =>
        Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue("tenant_id")!);

    // A student-role token carries a role claim that is not a StaffRole; resolve those to None rather than
    // throwing, so RequirePermission denies them (ForRole(None) is empty) and the audit interceptor still runs.
    public StaffRole Role =>
        Enum.TryParse<StaffRole>(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role), out var role)
            ? role
            : StaffRole.None;

    public string? DeviceId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue("device_id");

    public string ActorType
    {
        get
        {
            if (!IsAuthenticated)
                return "System";

            var roleClaim = httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.Role);
            return string.Equals(roleClaim, "Student", StringComparison.OrdinalIgnoreCase) ? "Student" : "Staff";
        }
    }
}
