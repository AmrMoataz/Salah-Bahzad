using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a student answers the last unanswered question and the assignment is auto-graded
/// (FR-PLAT-ASG-006). Its handler writes <c>Attendance.AssignmentScore</c> (<see cref="Percent"/>,
/// FR-PLAT-ATT-002). Attributed to the <b>System</b> actor (<see cref="ISystemActorAuditEvent"/>,
/// FR-PLAT-AUD-005) even though it fires inside the answering student's request — the grade is the
/// platform's act, not the student's.
/// </summary>
public sealed record AssignmentGradedEvent(
    Guid UserAssignmentId, Guid StudentId, Guid SessionId, int Percent, int ScoreMarks, int MaxMarks)
    : ISystemActorAuditEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "AssignmentGraded";
    public string AuditSummary => $"Assignment auto-graded: {Percent}% ({ScoreMarks}/{MaxMarks} marks).";
}
