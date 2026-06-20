using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Quizzes.DTOs;

namespace SalahBahazad.Application.Features.Quizzes.Commands.StartQuizAttempt;

internal sealed class StartQuizAttemptHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock)
    : IRequestHandler<StartQuizAttemptCommand, QuizAttemptDto>
{
    public async ValueTask<QuizAttemptDto> Handle(
        StartQuizAttemptCommand command, CancellationToken cancellationToken)
    {
        // Tracked load (owned attempts auto-include) so the new attempt + grade fields persist.
        var quiz = await db.UserQuizzes
            .FirstOrDefaultAsync(q => q.Id == command.QuizId, cancellationToken)
            ?? throw new NotFoundException("Quiz", command.QuizId);

        // IDOR (NFR-SEC-007): only the owning student may start an attempt.
        if (quiz.StudentId != currentUser.UserId)
            throw new ForbiddenException("This quiz belongs to another student.");

        // Translate the domain start-guards into 409s (contract §A #2).
        if (quiz.ActiveAttempt is not null)
            throw new ConflictException("An attempt is already in progress for this quiz.");
        if (quiz.AttemptsRemaining == 0)
            throw new ConflictException("No quiz attempts remain.");

        // Draw an independently randomised subset of the prerequisite's quiz-eligible bank (FR-PLAT-QZ-003).
        var eligible = await db.Questions
            .Include(q => q.Variations)
            .Where(q => q.SessionId == quiz.SourceSessionId && q.IsValidForQuiz)
            .ToListAsync(cancellationToken);
        if (eligible.Count == 0)
            throw new ConflictException("This quiz has no available questions.");

        var draws = QuizQuestionSelector.Draw(eligible, quiz.QuestionCount, Random.Shared);

        var now = clock.GetUtcNow();
        var attempt = quiz.StartAttempt(draws, now);

        await db.SaveChangesAsync(cancellationToken);

        // The auto-submit timer is scheduled by ScheduleQuizTimerHandler post-commit (the started event).
        return await attempt.ToAttemptDtoAsync(now, fileStorage, cancellationToken);
    }
}
