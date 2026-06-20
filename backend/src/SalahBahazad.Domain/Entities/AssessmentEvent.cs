using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// One in-assessment behaviour telemetry record (FR-PLAT-ASG-004/005) — entered/left/answered/navigated, with
/// an optional question position and duration. High-volume by design, so it lives in its own append-only table
/// (<c>assessment_events</c>), <b>not</b> the audit log (<see cref="IAuditViaEventOnly"/> with no semantic
/// event → the audit interceptor never writes a row for it). Tenant-scoped. Reused by 5B-2's quiz focus-loss
/// pings.
/// </summary>
public sealed class AssessmentEvent : TenantEntityBase, IAuditViaEventOnly
{
    private AssessmentEvent() { }

    /// <summary>The assignment (or, later, quiz attempt) this behaviour belongs to.</summary>
    public Guid UserAssignmentId { get; private set; }

    public AssessmentEventType Type { get; private set; }

    /// <summary>The one-based question position involved (for Answered/Navigated); null for Entered/Left.</summary>
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
}
