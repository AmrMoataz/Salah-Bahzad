using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Application.Features.Sessions.Queries.ListCatalogue;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// The student-portal catalogue read (S2, contract §A, FR-STU-CAT-001/002/004) — student-facing and
/// backend-only this slice (the redeem engine ships from Phase 4). Gated to a Student-role principal
/// (<see cref="RequireStudentExtensions"/>): anon → 401, staff → 403. The student/tenant are read from the
/// JWT; there is no URL id, so no IDOR surface. A pure read — not audited (parity with the other /api/me/* reads).
/// </summary>
internal sealed class MeCatalogueEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/catalogue")
            .WithTags("Catalogue")
            .WithOpenApi();

        group.MapGet("", ListAsync)
            .RequireStudent()
            .WithName("ListCatalogue")
            .WithSummary("The caller's published catalogue with per-caller enrollment state + prerequisite flags")
            .Produces<IReadOnlyList<CatalogueSessionDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] Guid? gradeId = null,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] Guid? specializationId = null)
        => Results.Ok(await sender.Send(
            new ListCatalogueQuery(search, gradeId, subjectId, specializationId), cancellationToken));
}
