using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Taxonomy.DTOs;
using SalahBahazad.Application.Features.Taxonomy.Grades.Commands.CreateGrade;
using SalahBahazad.Application.Features.Taxonomy.Grades.Commands.DeleteGrade;
using SalahBahazad.Application.Features.Taxonomy.Grades.Commands.UpdateGrade;
using SalahBahazad.Application.Features.Taxonomy.Grades.Queries.ListGrades;
using SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.CreateSpecialization;
using SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.DeleteSpecialization;
using SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.UpdateSpecialization;
using SalahBahazad.Application.Features.Taxonomy.Specializations.Queries.ListSpecializations;
using SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.CreateSubject;
using SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.DeleteSubject;
using SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.UpdateSubject;
using SalahBahazad.Application.Features.Taxonomy.Subjects.Queries.ListSubjects;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Taxonomy management endpoints — Grades, Subjects, Specializations (FR-PLAT-TAX-001/002/004,
/// FR-ADM-TAX-*). Reads require <c>TaxonomyRead</c>; writes are Teacher-only via
/// <c>TaxonomyCreate/Edit/Delete</c> (server-enforced, default-deny). All changes are audited automatically.
/// </summary>
internal sealed class TaxonomyEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/taxonomy")
            .WithTags("Taxonomy")
            .WithOpenApi();

        // ── Grades ────────────────────────────────────────────────────────────
        group.MapGet("/grades", ListGradesAsync)
            .RequirePermission(Permission.TaxonomyRead)
            .WithName("ListGrades")
            .Produces<IReadOnlyList<GradeDto>>();

        group.MapPost("/grades", CreateGradeAsync)
            .RequirePermission(Permission.TaxonomyCreate)
            .WithName("CreateGrade")
            .Produces<GradeDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/grades/{id:guid}", UpdateGradeAsync)
            .RequirePermission(Permission.TaxonomyEdit)
            .WithName("UpdateGrade")
            .Produces<GradeDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/grades/{id:guid}", DeleteGradeAsync)
            .RequirePermission(Permission.TaxonomyDelete)
            .WithName("DeleteGrade")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // ── Subjects ──────────────────────────────────────────────────────────
        group.MapGet("/subjects", ListSubjectsAsync)
            .RequirePermission(Permission.TaxonomyRead)
            .WithName("ListSubjects")
            .Produces<IReadOnlyList<SubjectDto>>();

        group.MapPost("/subjects", CreateSubjectAsync)
            .RequirePermission(Permission.TaxonomyCreate)
            .WithName("CreateSubject")
            .Produces<SubjectDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/subjects/{id:guid}", UpdateSubjectAsync)
            .RequirePermission(Permission.TaxonomyEdit)
            .WithName("UpdateSubject")
            .Produces<SubjectDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/subjects/{id:guid}", DeleteSubjectAsync)
            .RequirePermission(Permission.TaxonomyDelete)
            .WithName("DeleteSubject")
            .WithSummary("Soft-delete a subject (blocked with 409 while it has specializations)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // ── Specializations ─────────────────────────────────────────────────────
        group.MapGet("/specializations", ListSpecializationsAsync)
            .RequirePermission(Permission.TaxonomyRead)
            .WithName("ListSpecializations")
            .WithSummary("List specializations, optionally filtered by subjectId")
            .Produces<IReadOnlyList<SpecializationDto>>();

        group.MapPost("/specializations", CreateSpecializationAsync)
            .RequirePermission(Permission.TaxonomyCreate)
            .WithName("CreateSpecialization")
            .Produces<SpecializationDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/specializations/{id:guid}", UpdateSpecializationAsync)
            .RequirePermission(Permission.TaxonomyEdit)
            .WithName("UpdateSpecialization")
            .Produces<SpecializationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/specializations/{id:guid}", DeleteSpecializationAsync)
            .RequirePermission(Permission.TaxonomyDelete)
            .WithName("DeleteSpecialization")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    // ── Grades ──────────────────────────────────────────────────────────────
    private static async Task<IResult> ListGradesAsync(ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ListGradesQuery(), cancellationToken));

    private static async Task<IResult> CreateGradeAsync(
        [FromBody] TaxonomyNameRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateGradeCommand(request.Name), cancellationToken);
        return Results.Created($"/api/taxonomy/grades/{result.Id}", result);
    }

    private static async Task<IResult> UpdateGradeAsync(
        Guid id, [FromBody] TaxonomyNameRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new UpdateGradeCommand(id, request.Name), cancellationToken));

    private static async Task<IResult> DeleteGradeAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteGradeCommand(id), cancellationToken);
        return Results.NoContent();
    }

    // ── Subjects ────────────────────────────────────────────────────────────
    private static async Task<IResult> ListSubjectsAsync(ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ListSubjectsQuery(), cancellationToken));

    private static async Task<IResult> CreateSubjectAsync(
        [FromBody] TaxonomyNameRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSubjectCommand(request.Name), cancellationToken);
        return Results.Created($"/api/taxonomy/subjects/{result.Id}", result);
    }

    private static async Task<IResult> UpdateSubjectAsync(
        Guid id, [FromBody] TaxonomyNameRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new UpdateSubjectCommand(id, request.Name), cancellationToken));

    private static async Task<IResult> DeleteSubjectAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSubjectCommand(id), cancellationToken);
        return Results.NoContent();
    }

    // ── Specializations ───────────────────────────────────────────────────────
    private static async Task<IResult> ListSpecializationsAsync(
        ISender sender, CancellationToken cancellationToken, [FromQuery] Guid? subjectId = null)
        => Results.Ok(await sender.Send(new ListSpecializationsQuery(subjectId), cancellationToken));

    private static async Task<IResult> CreateSpecializationAsync(
        [FromBody] CreateSpecializationRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSpecializationCommand(request.SubjectId, request.Name), cancellationToken);
        return Results.Created($"/api/taxonomy/specializations/{result.Id}", result);
    }

    private static async Task<IResult> UpdateSpecializationAsync(
        Guid id, [FromBody] CreateSpecializationRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new UpdateSpecializationCommand(id, request.SubjectId, request.Name), cancellationToken));

    private static async Task<IResult> DeleteSpecializationAsync(
        Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSpecializationCommand(id), cancellationToken);
        return Results.NoContent();
    }
}

/// <summary>Request body for naming a grade or subject.</summary>
internal sealed record TaxonomyNameRequest(string Name);

/// <summary>Request body for creating/updating a specialization (name + owning subject).</summary>
internal sealed record CreateSpecializationRequest(Guid SubjectId, string Name);
