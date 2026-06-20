using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Review.DTOs;
using SalahBahazad.Application.Features.Review.Queries.GetAssignmentBehaviour;
using SalahBahazad.Application.Features.Review.Queries.GetAssignmentReview;
using SalahBahazad.Application.Features.Review.Queries.GetQuizBehaviour;
using SalahBahazad.Application.Features.Review.Queries.GetQuizReview;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Admin assignment + behaviour review (contract §C #7–8, scrReview, FR-ADM-REV-001/003). Staff-only
/// (<see cref="Permission.AttendanceRead"/>) — shows the correct option and per-question correctness, unlike the
/// student §A shape. The Quiz-attempts tab is 5B-2 (no endpoint here). Default-deny, server-enforced.
/// </summary>
internal sealed class ReviewEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/review")
            .WithTags("Review")
            .WithOpenApi();

        group.MapGet("/assignments/{enrollmentId:guid}", GetReviewAsync)
            .RequirePermission(Permission.AttendanceRead)
            .WithName("GetAssignmentReview")
            .WithSummary("Per-question submitted-vs-correct review, with score and time")
            .Produces<AssignmentReviewDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/assignments/{enrollmentId:guid}/behaviour", GetBehaviourAsync)
            .RequirePermission(Permission.AttendanceRead)
            .WithName("GetAssignmentBehaviour")
            .WithSummary("The in-assessment behaviour timeline")
            .Produces<IReadOnlyList<BehaviourEventDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/quizzes/{enrollmentId:guid}", GetQuizReviewAsync)
            .RequirePermission(Permission.AttendanceRead)
            .WithName("GetQuizReview")
            .WithSummary("The Quiz-attempts review: best-of, pass, attempts (the best marked) — FR-ADM-REV-002")
            .Produces<QuizReviewDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/quizzes/{enrollmentId:guid}/behaviour", GetQuizBehaviourAsync)
            .RequirePermission(Permission.AttendanceRead)
            .WithName("GetQuizBehaviour")
            .WithSummary("The quiz attempts' focus-loss/return behaviour timeline")
            .Produces<IReadOnlyList<BehaviourEventDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetReviewAsync(
        Guid enrollmentId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetAssignmentReviewQuery(enrollmentId), cancellationToken));

    private static async Task<IResult> GetBehaviourAsync(
        Guid enrollmentId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetAssignmentBehaviourQuery(enrollmentId), cancellationToken));

    private static async Task<IResult> GetQuizReviewAsync(
        Guid enrollmentId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetQuizReviewQuery(enrollmentId), cancellationToken));

    private static async Task<IResult> GetQuizBehaviourAsync(
        Guid enrollmentId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetQuizBehaviourQuery(enrollmentId), cancellationToken));
}
