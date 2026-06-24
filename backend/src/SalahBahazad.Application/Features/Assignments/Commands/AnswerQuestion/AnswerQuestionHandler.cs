using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Assignments.DTOs;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Assignments.Commands.AnswerQuestion;

internal sealed class AnswerQuestionHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, TimeProvider clock, IStudentPlanCache planCache)
    : IRequestHandler<AnswerQuestionCommand, AssignmentProgressDto>
{
    public async ValueTask<AssignmentProgressDto> Handle(
        AnswerQuestionCommand command, CancellationToken cancellationToken)
    {
        // Tracked load (owned questions/options auto-include) so the answer + grade persist.
        var assignment = await db.UserAssignments
            .FirstOrDefaultAsync(a => a.Id == command.AssignmentId, cancellationToken)
            ?? throw new NotFoundException("Assignment", command.AssignmentId);

        // IDOR (NFR-SEC-007): only the owning student may answer.
        if (assignment.StudentId != currentUser.UserId)
            throw new ForbiddenException("This assignment belongs to another student.");

        // Translate domain guards into the right HTTP codes (already-complete → 409, unknown refs → 404).
        if (assignment.Status == AssignmentStatus.Completed)
            throw new ConflictException("This assignment is already completed and cannot be changed.");

        var question = assignment.Questions.FirstOrDefault(q => q.Id == command.AssignmentQuestionId)
            ?? throw new NotFoundException("Question", command.AssignmentQuestionId);

        if (question.Options.All(o => o.Id != command.SelectedOptionId))
            throw new NotFoundException("Option", command.SelectedOptionId);

        var now = clock.GetUtcNow();
        var answeredOrder = assignment.Answer(command.AssignmentQuestionId, command.SelectedOptionId, now);

        // Behaviour telemetry → assessment_events (high-volume), never the audit log (FR-PLAT-ASG-004).
        db.AssessmentEvents.Add(AssessmentEvent.Create(
            assignment.TenantId, assignment.Id, AssessmentEventType.Answered, now, answeredOrder));

        await db.SaveChangesAsync(cancellationToken);

        // A non-final answer moves the Home plan's assignment progress but raises no domain event (only the last
        // answer raises AssignmentGraded, handled separately), so drop the cached plan inline (contract §D). The
        // final answer also invalidates via that event — a harmless double drop. Off the critical path.
        await planCache.InvalidateAsync(currentUser.UserId, cancellationToken);

        return assignment.ToProgressDto();
    }
}
