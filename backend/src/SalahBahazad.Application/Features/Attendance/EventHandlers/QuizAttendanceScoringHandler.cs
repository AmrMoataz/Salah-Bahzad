using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Application.Features.Attendance.EventHandlers;

/// <summary>
/// Writes the best-of quiz percent onto the student's <c>Attendance</c> for the gated session when a quiz attempt
/// is graded (FR-PLAT-QZ-007, FR-PLAT-ATT-002). Runs post-commit (the grade event dispatches after the submit /
/// timeout transaction). The single semantic audit row for that grade is carried by the
/// start/submit/timeout event on the attempt (FR-PLAT-QZ-010); this attendance write is <c>IAuditViaEventOnly</c>
/// and adds none. Mirrors the assignment <see cref="AttendanceScoringHandler"/>.
/// </summary>
internal sealed class QuizAttendanceScoringHandler(IAppDbContext db)
    : INotificationHandler<QuizGradedEvent>
{
    public async ValueTask Handle(QuizGradedEvent notification, CancellationToken cancellationToken)
    {
        var attendance = await db.Attendances.FirstOrDefaultAsync(
            a => a.StudentId == notification.StudentId && a.SessionId == notification.GatedSessionId,
            cancellationToken);

        if (attendance is null)
            return; // The shell is created on enrol; nothing to score if it is somehow absent.

        attendance.SetBestQuizPercent(notification.BestPercent);
        await db.SaveChangesAsync(cancellationToken);
    }
}
