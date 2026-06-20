using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Quizzes.Commands.RecordQuizFocusEvent;

internal sealed class RecordQuizFocusEventHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<RecordQuizFocusEventCommand, Unit>
{
    public async ValueTask<Unit> Handle(RecordQuizFocusEventCommand command, CancellationToken cancellationToken)
    {
        // Resolve the quiz owning the attempt; tenant scoping is the global filter (404 cross-tenant).
        var quiz = await db.UserQuizzes
            .AsNoTracking()
            .Where(q => q.Attempts.Any(a => a.Id == command.AttemptId))
            .Select(q => new { q.StudentId, q.TenantId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Attempt", command.AttemptId);

        // IDOR (NFR-SEC-007): only the owning student may record behaviour against their attempt.
        if (quiz.StudentId != currentUser.UserId)
            throw new ForbiddenException("This quiz belongs to another student.");

        // Monitoring only — recorded to assessment_events, NEVER forfeits the attempt (FR-PLAT-QZ-006).
        db.AssessmentEvents.Add(AssessmentEvent.CreateForQuiz(
            quiz.TenantId, command.AttemptId, command.Type, command.OccurredAtUtc, command.DurationMs));

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
