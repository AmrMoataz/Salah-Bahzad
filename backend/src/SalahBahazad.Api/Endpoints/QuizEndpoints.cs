using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Quizzes.Commands.AnswerQuizQuestion;
using SalahBahazad.Application.Features.Quizzes.Commands.RecordQuizFocusEvent;
using SalahBahazad.Application.Features.Quizzes.Commands.StartQuizAttempt;
using SalahBahazad.Application.Features.Quizzes.Commands.SubmitQuizAttempt;
using SalahBahazad.Application.Features.Quizzes.DTOs;
using SalahBahazad.Application.Features.Quizzes.Queries.GetMyQuiz;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// The proctored-quiz <b>engine</b> (contract §A #1–5, FR-PLAT-QZ-001..010) — student-facing and backend-only
/// (no admin screen; the future student portal/app calls it). Every route is gated to a Student-role principal
/// (<see cref="RequireStudentExtensions"/>): anon → 401, staff → 403. The student/tenant are read from the JWT;
/// handlers IDOR-check ownership. No <c>isCorrect</c> is exposed here.
/// </summary>
internal sealed class QuizEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/quizzes")
            .WithTags("Quizzes")
            .WithOpenApi();

        group.MapGet("/by-session/{sessionId:guid}", GetBySessionAsync)
            .RequireStudent()
            .WithName("GetMyQuiz")
            .WithSummary("The caller's gating quiz for a session (summary; no questions/answers)")
            .Produces<StudentQuizDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{quizId:guid}/attempts", StartAttemptAsync)
            .RequireStudent()
            .WithName("StartQuizAttempt")
            .WithSummary("Start an attempt (randomised question set); 409 if exhausted or one is active")
            .Produces<QuizAttemptDto>()
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/attempts/{attemptId:guid}/questions/{aqId:guid}/answer", AnswerAsync)
            .RequireStudent()
            .WithName("AnswerQuizQuestion")
            .WithSummary("Record an answer; 409 if the attempt is not in progress or past its deadline")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/attempts/{attemptId:guid}/submit", SubmitAsync)
            .RequireStudent()
            .WithName("SubmitQuizAttempt")
            .WithSummary("Grade and submit the attempt; updates best-of and pass state")
            .Produces<QuizAttemptResultDto>()
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/attempts/{attemptId:guid}/focus", RecordFocusAsync)
            .RequireStudent()
            .WithName("RecordQuizFocusEvent")
            .WithSummary("Record a focus-loss/return event (monitoring only — never auto-forfeits)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetBySessionAsync(
        Guid sessionId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyQuizQuery(sessionId), cancellationToken));

    private static async Task<IResult> StartAttemptAsync(
        Guid quizId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new StartQuizAttemptCommand(quizId), cancellationToken));

    private static async Task<IResult> AnswerAsync(
        Guid attemptId,
        Guid aqId,
        [FromBody] AnswerQuizRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new AnswerQuizQuestionCommand(attemptId, aqId, request.SelectedOptionId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SubmitAsync(
        Guid attemptId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new SubmitQuizAttemptCommand(attemptId), cancellationToken));

    private static async Task<IResult> RecordFocusAsync(
        Guid attemptId,
        [FromBody] QuizFocusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new RecordQuizFocusEventCommand(attemptId, request.Type, request.OccurredAtUtc, request.DurationMs),
            cancellationToken);
        return Results.NoContent();
    }
}

/// <summary>Request body for recording an answer (#3).</summary>
internal sealed record AnswerQuizRequest(Guid SelectedOptionId);

/// <summary>Request body for a quiz focus event (#5): FocusLost | FocusReturned, when, and optional duration.</summary>
internal sealed record QuizFocusRequest(AssessmentEventType Type, DateTimeOffset OccurredAtUtc, int? DurationMs);
