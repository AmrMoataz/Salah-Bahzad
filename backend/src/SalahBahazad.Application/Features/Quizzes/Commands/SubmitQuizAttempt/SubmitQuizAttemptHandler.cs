using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Quizzes.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Quizzes.Commands.SubmitQuizAttempt;

internal sealed class SubmitQuizAttemptHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, TimeProvider clock)
    : IRequestHandler<SubmitQuizAttemptCommand, QuizAttemptResultDto>
{
    public async ValueTask<QuizAttemptResultDto> Handle(
        SubmitQuizAttemptCommand command, CancellationToken cancellationToken)
    {
        var quiz = await db.UserQuizzes
            .FirstOrDefaultAsync(q => q.Attempts.Any(a => a.Id == command.AttemptId), cancellationToken)
            ?? throw new NotFoundException("Attempt", command.AttemptId);

        // IDOR (NFR-SEC-007): only the owning student may submit.
        if (quiz.StudentId != currentUser.UserId)
            throw new ForbiddenException("This quiz belongs to another student.");

        var attempt = quiz.Attempts.First(a => a.Id == command.AttemptId);
        if (attempt.Status != QuizAttemptStatus.InProgress)
            throw new ConflictException("This quiz attempt has already ended and cannot be submitted.");

        quiz.SubmitAttempt(command.AttemptId, clock.GetUtcNow());

        await db.SaveChangesAsync(cancellationToken);

        // Post-commit: the submitted event cancels the timer; the grade event writes Attendance.BestQuizPercent.
        return quiz.ToResultDto(attempt);
    }
}
