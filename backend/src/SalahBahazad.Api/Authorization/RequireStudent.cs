using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Api.Authorization;

/// <summary>
/// Minimal-API endpoint filter gating an endpoint to an authenticated <b>Student-role</b> principal — the one
/// new auth touch this phase, used only by the student redeem path (#12, FR-PLAT-ENR-001). Staff tokens are
/// rejected with 403 and anonymous callers with 401, mirroring <see cref="RequirePermissionFilter"/>; the
/// student/tenant identity is then read from the JWT by the handler.
/// </summary>
internal sealed class RequireStudentFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserResolver>();

        if (!currentUser.IsAuthenticated)
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized.");

        if (currentUser.ActorType != "Student")
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden.",
                detail: "This action requires a student account.");

        return await next(context);
    }
}

internal static class RequireStudentExtensions
{
    /// <summary>Requires authentication (→ 401) and a Student-role principal (→ 403 for staff).</summary>
    public static TBuilder RequireStudent<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.AddEndpointFilter(new RequireStudentFilter());
        return builder;
    }
}
