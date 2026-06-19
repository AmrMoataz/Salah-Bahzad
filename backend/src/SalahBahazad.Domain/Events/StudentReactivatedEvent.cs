using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a deactivated student account is re-activated by staff (FR-ADM-STU-006). Lets the
/// audit interceptor record a semantic entry for the lifecycle transition (FR-ADM-STU-010).
/// </summary>
public sealed record StudentReactivatedEvent(Guid StudentId) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "StudentReactivated";
    public string AuditSummary => "Student account reactivated.";
}
