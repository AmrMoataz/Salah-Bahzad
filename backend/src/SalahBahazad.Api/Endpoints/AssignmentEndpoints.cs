using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Assignments.Commands.AnswerQuestion;
using SalahBahazad.Application.Features.Assignments.Commands.RecordAssessmentEvents;
using SalahBahazad.Application.Features.Assignments.DTOs;
using SalahBahazad.Application.Features.Assignments.Queries.GetMyAssignment;
using SalahBahazad.Application.Features.Assignments.Queries.GetMyAssignmentReview;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// The open-book assignment <b>engine</b> (contract §A #1–3, FR-PLAT-ASG-001..006) — student-facing and
/// backend-only this phase (no admin screen, like the Phase-4 redeem path). Every route is gated to a
/// Student-role principal (<see cref="RequireStudentExtensions"/>): anon → 401, staff → 403. The student and
/// tenant are read from the JWT; handlers IDOR-check ownership. No <c>isCorrect</c> is exposed here.
/// </summary>
internal sealed class AssignmentEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/assignments")
            .WithTags("Assignments")
            .WithOpenApi();

        group.MapGet("/by-session/{sessionId:guid}", GetBySessionAsync)
            .RequireStudent()
            .WithName("GetMyAssignment")
            .WithSummary("The caller's assignment for a session (resumable; no correct answers exposed)")
            .Produces<StudentAssignmentDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{assignmentId:guid}/questions/{aqId:guid}/answer", AnswerAsync)
            .RequireStudent()
            .WithName("AnswerAssignmentQuestion")
            .WithSummary("Record an answer; auto-grades when the last question is answered")
            .Produces<AssignmentProgressDto>()
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{assignmentId:guid}/events", RecordEventsAsync)
            .RequireStudent()
            .WithName("RecordAssessmentEvents")
            .WithSummary("Append an in-assessment behaviour event and accrue time")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // The ONLY student surface exposing correctness, gated to the caller's own Completed assignment (§B).
        group.MapGet("/{assignmentId:guid}/review", GetReviewAsync)
            .RequireStudent()
            .WithName("GetMyAssignmentReview")
            .WithSummary("The caller's own completed assignment with the answer key and score")
            .Produces<StudentAssignmentReviewDto>()
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetBySessionAsync(
        Guid sessionId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyAssignmentQuery(sessionId), cancellationToken));

    private static async Task<IResult> GetReviewAsync(
        Guid assignmentId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyAssignmentReviewQuery(assignmentId), cancellationToken));

    private static async Task<IResult> AnswerAsync(
        Guid assignmentId,
        Guid aqId,
        [FromBody] AnswerQuestionRequest request,
        ISender sender,
        CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new AnswerQuestionCommand(assignmentId, aqId, request.SelectedOptionId), cancellationToken));

    private static async Task<IResult> RecordEventsAsync(
        Guid assignmentId,
        [FromBody] RecordEventRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new RecordAssessmentEventsCommand(
                assignmentId, request.Type, request.QuestionOrder, request.OccurredAtUtc, request.ElapsedMs),
            cancellationToken);
        return Results.NoContent();
    }
}

/// <summary>Request body for recording an answer (#2).</summary>
internal sealed record AnswerQuestionRequest(Guid SelectedOptionId);

/// <summary>Request body for an in-assessment behaviour event (#3).</summary>
internal sealed record RecordEventRequest(
    AssessmentEventType Type, int? QuestionOrder, DateTimeOffset OccurredAtUtc, int? ElapsedMs);
