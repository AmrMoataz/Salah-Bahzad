using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Application.Features.Sessions.Queries.GetMyMaterialUrl;
using SalahBahazad.Application.Features.Sessions.Queries.GetMySession;
using SalahBahazad.Application.Features.Sessions.Queries.ListMySessions;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// The student-portal My-Sessions reads (S3, contract §A/§B/§C, FR-STU-SES-001..004) — student-facing and
/// backend-only this slice. Every route is gated to a Student-role principal (<see cref="RequireStudentExtensions"/>):
/// anon → 401, staff → 403. The student/tenant are read from the JWT; the <c>{id}</c> is a session id whose
/// ownership is the caller's own enrollment (a non-enrolled / cross-tenant / refunded id → 404, no IDOR surface).
/// Pure reads — not audited (parity with the other /api/me/* reads). The 5C playback gate (POST
/// /api/me/videos/{id}/playback) is a separate group, reused as-is.
/// </summary>
internal sealed class MeSessionsEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/sessions")
            .WithTags("My Sessions")
            .WithOpenApi();

        group.MapGet("", ListAsync)
            .RequireStudent()
            .WithName("ListMySessions")
            .WithSummary("List the caller's enrolled sessions with progress + expiry")
            .Produces<IReadOnlyList<MySessionDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetAsync)
            .RequireStudent()
            .WithName("GetMySession")
            .WithSummary("One enrolled session's detail: playlist, materials, assignment & quiz status")
            .Produces<MySessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/materials/{materialId:guid}/url", GetMaterialUrlAsync)
            .RequireStudent()
            .WithName("GetMySessionMaterialUrl")
            .WithSummary("Short-lived signed URL for one material of an enrolled session")
            .Produces<SignedUrlDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? state = null)
    {
        // Parse the filter chip leniently — an unrecognised value means "no filter" (contract §A.1).
        MySessionState? parsed = Enum.TryParse<MySessionState>(state, ignoreCase: true, out var value)
            ? value
            : null;
        return Results.Ok(await sender.Send(new ListMySessionsQuery(parsed), cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMySessionQuery(id), cancellationToken));

    private static async Task<IResult> GetMaterialUrlAsync(
        Guid id, Guid materialId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyMaterialUrlQuery(id, materialId), cancellationToken));
}
