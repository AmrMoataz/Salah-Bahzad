using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Application.Features.Attendance.EventHandlers;

/// <summary>
/// Writes the auto-graded assignment percent onto the student's <c>Attendance</c> record when an assignment is
/// graded (FR-PLAT-ASG-006, FR-PLAT-ATT-002). Runs post-commit (the grade event dispatches after the answer
/// transaction). The grade's single audit row is carried by the System-attributed <see cref="AssignmentGradedEvent"/>
/// on the assignment itself (FR-PLAT-AUD-005); this attendance write is <c>IAuditViaEventOnly</c> and adds none.
/// </summary>
internal sealed class AttendanceScoringHandler(IAppDbContext db)
    : INotificationHandler<AssignmentGradedEvent>
{
    public async ValueTask Handle(AssignmentGradedEvent notification, CancellationToken cancellationToken)
    {
        var attendance = await db.Attendances.FirstOrDefaultAsync(
            a => a.StudentId == notification.StudentId && a.SessionId == notification.SessionId,
            cancellationToken);

        if (attendance is null)
            return; // The shell is created on enrol; nothing to score if it is somehow absent.

        attendance.SetAssignmentScore(notification.Percent);
        await db.SaveChangesAsync(cancellationToken);
    }
}
