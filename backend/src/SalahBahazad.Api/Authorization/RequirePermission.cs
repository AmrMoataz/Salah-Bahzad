using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Authorization;

/// <summary>
/// Minimal-API endpoint filter enforcing a single granular <see cref="Permission"/> (FR-PLAT-AUTH-007/008,
/// default-deny, NFR-SEC-003). The platform JWT carries only the role; permissions are expanded
/// server-side via <see cref="PermissionCatalog"/> — UI hiding is never the only control.
/// </summary>
internal sealed class RequirePermissionFilter(Permission permission) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserResolver>();

        if (!currentUser.IsAuthenticated)
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized.");

        if (!PermissionCatalog.ForRole(currentUser.Role).Contains(permission))
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden.",
                detail: $"This action requires the '{permission}' permission.");

        return await next(context);
    }
}

internal static class RequirePermissionExtensions
{
    /// <summary>
    /// Requires authentication (→ 401 when missing) and the given permission (→ 403 when absent).
    /// Usage: <c>group.MapGet("/", Handler).RequirePermission(Permission.StaffRead)</c>.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.AddEndpointFilter(new RequirePermissionFilter(permission));
        return builder;
    }
}
