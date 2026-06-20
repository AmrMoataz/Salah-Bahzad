using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a student's assignment snapshot is generated on enrolment (FR-PLAT-ASG-001). Attributed to the
/// <b>System</b> actor (<see cref="ISystemActorAuditEvent"/>, FR-PLAT-AUD-005): generation runs inside the
/// enrolling student's redeem (#12) or staff unlock (#9) request, but the platform — not that principal —
/// performs it.
/// </summary>
public sealed record AssignmentGeneratedEvent(
    Guid UserAssignmentId, Guid StudentId, Guid SessionId, int QuestionCount) : ISystemActorAuditEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "AssignmentGenerated";
    public string AuditSummary => $"Assignment generated with {QuestionCount} question(s).";
}
