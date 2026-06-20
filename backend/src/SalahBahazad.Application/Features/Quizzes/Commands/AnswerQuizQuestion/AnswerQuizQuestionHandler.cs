using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Quizzes.Commands.AnswerQuizQuestion;

internal sealed class AnswerQuizQuestionHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, TimeProvider clock)
    : IRequestHandler<AnswerQuizQuestionCommand, Unit>
{
    public async ValueTask<Unit> Handle(AnswerQuizQuestionCommand command, CancellationToken cancellationToken)
    {
        // Tracked load of the quiz owning the attempt (owned attempts/questions/options auto-include).
        var quiz = await db.UserQuizzes
            .FirstOrDefaultAsync(q => q.Attempts.Any(a => a.Id == command.AttemptId), cancellationToken)
            ?? throw new NotFoundException("Attempt", command.AttemptId);

        // IDOR (NFR-SEC-007): only the owning student may answer.
        if (quiz.StudentId != currentUser.UserId)
            throw new ForbiddenException("This quiz belongs to another student.");

        var attempt = quiz.Attempts.First(a => a.Id == command.AttemptId);

        // Translate domain guards into the right HTTP codes (terminal/past-deadline → 409, unknown refs → 404).
        var now = clock.GetUtcNow();
        if (attempt.Status != QuizAttemptStatus.InProgress)
            throw new ConflictException("This quiz attempt is no longer in progress.");
        if (now > attempt.DeadlineUtc)
            throw new ConflictException("This quiz attempt's time limit has elapsed.");

        var question = attempt.Questions.FirstOrDefault(q => q.Id == command.AttemptQuestionId)
            ?? throw new NotFoundException("Question", command.AttemptQuestionId);
        if (question.Options.All(o => o.Id != command.SelectedOptionId))
            throw new NotFoundException("Option", command.SelectedOptionId);

        quiz.Answer(command.AttemptId, command.AttemptQuestionId, command.SelectedOptionId, now);

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
