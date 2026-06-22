using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Quizzes.DTOs;

namespace SalahBahazad.Application.Features.Quizzes.Queries.GetMyQuizAttemptReview;

internal sealed class GetMyQuizAttemptReviewHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage)
    : IRequestHandler<GetMyQuizAttemptReviewQuery, StudentQuizAttemptReviewDto>
{
    public async ValueTask<StudentQuizAttemptReviewDto> Handle(
        GetMyQuizAttemptReviewQuery query, CancellationToken cancellationToken)
    {
        // IDOR (NFR-SEC-007): resolve the attempt THROUGH its owning UserQuiz — ownership is the root's StudentId,
        // never the URL id. Folding StudentId into the predicate means an unknown / another student's / another
        // tenant's attempt id all resolve to null → an opaque 404 (never reveal existence; the global query filter
        // supplies the tenant scope, NFR-SEC-010). Owned attempts/questions/options auto-include with the root.
        var quiz = await db.UserQuizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                q => q.StudentId == currentUser.UserId && q.Attempts.Any(a => a.Id == query.AttemptId),
                cancellationToken)
            ?? throw new NotFoundException("Quiz attempt", query.AttemptId);

        var attempt = quiz.Attempts.First(a => a.Id == query.AttemptId);

        // The answer key is revealed only after the sitting ends (FR-STU-QZ-009, contract §B.2) — never mid-attempt
        // (there is at most one InProgress attempt, the active sitting).
        if (attempt.IsInProgress)
            throw new ForbiddenException(
                "Finish the quiz to see your answers and score.", "quiz_attempt_in_progress");

        // The title ignores the query filters so an archived/soft-deleted session still resolves (mirrors the S4
        // assignment review + the staff quiz review).
        var sessionTitle = await db.Sessions
            .IgnoreQueryFilters()
            .Where(s => s.Id == quiz.GatedSessionId)
            .Select(s => s.Title)
            .FirstOrDefaultAsync(cancellationToken);

        return await quiz.ToReviewDtoAsync(attempt, sessionTitle, fileStorage, cancellationToken);
    }
}
