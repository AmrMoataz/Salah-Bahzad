using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Application.Features.Sessions.Queries.GetMyPlan;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// The student-portal Home read (contract §A, FR-STU-SES-001 et al.) — student-facing and backend-only. The one
/// route is gated to a Student-role principal (<see cref="RequireStudentExtensions"/>): anon → 401, staff → 403.
/// The student/tenant are read from the JWT; there is no path parameter, so no IDOR surface. A pure read — not
/// audited (parity with the other /api/me/* reads) — and always 200 (an onboarding plan when the caller has no
/// enrollments, never 404). Served from the Redis-backed HybridCache when warm; computed on a miss (§C).
/// </summary>
internal sealed class MeHomeEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me")
            .WithTags("My Home")
            .WithOpenApi();

        group.MapGet("/plan", GetPlanAsync)
            .RequireStudent()
            .WithName("GetMyPlan")
            .WithSummary("The caller's derived weekly study plan: KPIs, focus session, gate-ordered steps")
            .Produces<MyPlanDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetPlanAsync(ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyPlanQuery(), cancellationToken));
}
