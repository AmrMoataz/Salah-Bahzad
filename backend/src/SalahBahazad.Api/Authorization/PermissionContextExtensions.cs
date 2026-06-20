using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Authorization;

/// <summary>
/// Lets an endpoint branch on a finer-grained permission than the one its <see cref="RequirePermissionFilter"/>
/// already enforced — e.g. <c>GET /api/audit</c> requires <c>AuditRead</c> for all, then widens the result for
/// callers who also hold <c>AuditReadSensitive</c>. Same role→permission expansion as the filter
/// (<see cref="PermissionCatalog"/>), so UI hiding is never the only control (NFR-SEC-003).
/// </summary>
internal static class PermissionContextExtensions
{
    public static bool HasPermission(this HttpContext http, Permission permission)
    {
        var currentUser = http.RequestServices.GetRequiredService<ICurrentUserResolver>();
        return currentUser.IsAuthenticated
            && PermissionCatalog.ForRole(currentUser.Role).Contains(permission);
    }
}
