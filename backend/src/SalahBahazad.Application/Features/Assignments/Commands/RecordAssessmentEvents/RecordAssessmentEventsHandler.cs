using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Assignments.Commands.RecordAssessmentEvents;

internal sealed class RecordAssessmentEventsHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<RecordAssessmentEventsCommand, Unit>
{
    public async ValueTask<Unit> Handle(
        RecordAssessmentEventsCommand command, CancellationToken cancellationToken)
    {
        var assignment = await db.UserAssignments
            .FirstOrDefaultAsync(a => a.Id == command.AssignmentId, cancellationToken)
            ?? throw new NotFoundException("Assignment", command.AssignmentId);

        // IDOR (NFR-SEC-007): only the owning student may record behaviour against their assignment.
        if (assignment.StudentId != currentUser.UserId)
            throw new ForbiddenException("This assignment belongs to another student.");

        db.AssessmentEvents.Add(AssessmentEvent.Create(
            assignment.TenantId,
            assignment.Id,
            command.Type,
            command.OccurredAtUtc,
            command.QuestionOrder,
            command.ElapsedMs));

        if (command.ElapsedMs is int ms && ms > 0)
            assignment.AddTime(ms / 1000);

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
