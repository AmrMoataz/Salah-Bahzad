using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a student's gating-quiz is generated on enrolment from the prerequisite's bank+settings
/// (FR-PLAT-QZ-001). Attributed to the <b>System</b> actor (<see cref="ISystemActorAuditEvent"/>,
/// FR-PLAT-AUD-005): generation runs inside the enrolling student's redeem (#12) or staff unlock (#9) request,
/// but the platform — not that principal — performs it. Mirrors <see cref="AssignmentGeneratedEvent"/>.
/// </summary>
public sealed record QuizGeneratedEvent(
    Guid UserQuizId, Guid StudentId, Guid GatedSessionId, Guid SourceSessionId) : ISystemActorAuditEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "QuizGenerated";
    public string AuditSummary => "Gating quiz generated from the prerequisite's question bank.";
}
