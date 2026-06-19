using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a pending student is rejected (FR-ADM-STU-004). The mandatory <paramref name="Reason"/>
/// is carried into the audit <c>Summary</c> and is the source for the student-facing notification
/// (FR-PLAT-NOT-001) — the interceptor's field-diff records the state change but not the "why".
/// </summary>
public sealed record StudentRejectedEvent(Guid StudentId, string Reason) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "StudentRejected";
    public string AuditSummary => $"Student rejected: {Reason}";
}
