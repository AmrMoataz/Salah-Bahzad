using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised whenever a quiz attempt is graded (on submit or timeout) and the quiz's best-of percent may have
/// changed (FR-PLAT-QZ-007, FR-PLAT-ATT-002). Drives the attendance scorer to write
/// <c>Attendance.BestQuizPercent</c> for the gated session. <b>Not</b> an audit event: the semantic audit row is
/// the start/submit/timeout event on the attempt itself, so this carries no <see cref="IAuditableDomainEvent"/>
/// and the <c>IAuditViaEventOnly</c> attendance write adds none — mirrors how <see cref="AssignmentGradedEvent"/>
/// feeds the assignment score (there the grade event is itself the audit row; here the attempt event is).
/// </summary>
public sealed record QuizGradedEvent(Guid StudentId, Guid GatedSessionId, int BestPercent, bool Passed)
    : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
