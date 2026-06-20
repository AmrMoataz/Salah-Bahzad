using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Quizzes;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetQuizReview;

internal sealed class GetQuizReviewHandler(IAppDbContext db)
    : IRequestHandler<GetQuizReviewQuery, QuizReviewDto>
{
    public async ValueTask<QuizReviewDto> Handle(GetQuizReviewQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping is the global filter; 404 when the enrollment has no quiz in the caller's tenant.
        var quiz = await db.UserQuizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.EnrollmentId == query.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("Quiz", query.EnrollmentId);

        // The best attempt (highest score, earliest on a tie) is the one marked "best" in the review.
        var bestAttemptId = quiz.Attempts
            .Where(a => a.ScorePercent.HasValue)
            .OrderByDescending(a => a.ScorePercent!.Value)
            .ThenBy(a => a.Number)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefault();

        var attempts = quiz.Attempts
            .OrderBy(a => a.Number)
            .Select(a => new QuizReviewAttemptDto(
                a.Number,
                a.ScorePercent,
                a.TimeSpentSeconds,
                QuizAttemptFlag.For(a.Status),
                a.Status,
                a.StartedAtUtc,
                a.Id == bestAttemptId))
            .ToList();

        return new QuizReviewDto(
            quiz.BestPercent, quiz.Passed, quiz.MinPassPercent, quiz.AttemptsUsed, quiz.AttemptCount, attempts);
    }
}
