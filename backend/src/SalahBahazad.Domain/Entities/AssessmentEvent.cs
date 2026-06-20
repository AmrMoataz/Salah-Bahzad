using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// One in-assessment behaviour telemetry record (FR-PLAT-ASG-004/005, FR-PLAT-QZ-006) — entered/left/answered/
/// navigated for an assignment, or focus-lost/returned for a quiz attempt, with an optional question position
/// and duration. High-volume by design, so it lives in its own append-only table (<c>assessment_events</c>),
/// <b>not</b> the audit log (<see cref="IAuditViaEventOnly"/> with no semantic event → the audit interceptor
/// never writes a row for it). Tenant-scoped. Exactly one subject is set: <see cref="UserAssignmentId"/> for an
/// assignment event, <see cref="QuizAttemptId"/> for a quiz-attempt event.
/// </summary>
public sealed class AssessmentEvent : TenantEntityBase, IAuditViaEventOnly
{
    private AssessmentEvent() { }

    /// <summary>The assignment this behaviour belongs to; null for a quiz-attempt event.</summary>
    public Guid? UserAssignmentId { get; private set; }

    /// <summary>The quiz attempt this behaviour belongs to (FR-PLAT-QZ-006); null for an assignment event.</summary>
    public Guid? QuizAttemptId { get; private set; }

    public AssessmentEventType Type { get; private set; }

    /// <summary>The one-based question position involved (for Answered/Navigated); null otherwise.</summary>
    public int? QuestionOrder { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>Optional elapsed time the client attributes to this event (focus duration, think time).</summary>
    public int? DurationMs { get; private set; }

    public static AssessmentEvent Create(
        Guid tenantId,
        Guid userAssignmentId,
        AssessmentEventType type,
        DateTimeOffset occurredAtUtc,
        int? questionOrder = null,
        int? durationMs = null)
    {
        var behaviour = new AssessmentEvent
        {
            UserAssignmentId = userAssignmentId,
            Type = type,
            QuestionOrder = questionOrder,
            OccurredAtUtc = occurredAtUtc,
            DurationMs = durationMs is > 0 ? durationMs : null,
        };
        behaviour.SetTenant(tenantId);
        return behaviour;
    }

    /// <summary>
    /// Records a quiz-attempt behaviour event (FR-PLAT-QZ-006) — focus-loss/return monitoring that is
    /// <b>never</b> auto-forfeited. Keyed by the quiz attempt, not an assignment.
    /// </summary>
    public static AssessmentEvent CreateForQuiz(
        Guid tenantId,
        Guid quizAttemptId,
        AssessmentEventType type,
        DateTimeOffset occurredAtUtc,
        int? durationMs = null)
    {
        var behaviour = new AssessmentEvent
        {
            QuizAttemptId = quizAttemptId,
            Type = type,
            OccurredAtUtc = occurredAtUtc,
            DurationMs = durationMs is > 0 ? durationMs : null,
        };
        behaviour.SetTenant(tenantId);
        return behaviour;
    }
}
