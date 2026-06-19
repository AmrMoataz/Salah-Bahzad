using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when an active student account is deactivated by staff (FR-ADM-STU-006). Lets the audit
/// interceptor record a semantic entry for the lifecycle transition (FR-ADM-STU-010).
/// </summary>
public sealed record StudentDeactivatedEvent(Guid StudentId) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "StudentDeactivated";
    public string AuditSummary => "Student account deactivated.";
}
