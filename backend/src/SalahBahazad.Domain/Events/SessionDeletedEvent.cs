using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a session is soft-deleted (FR-ADM-SES-011, FR-PLAT-SES-009). Soft-delete preserves
/// enrollment/history; this semantic event makes the audit entry read meaningfully.
/// </summary>
public sealed record SessionDeletedEvent(Guid SessionId) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "SessionDeleted";
    public string AuditSummary => "Session deleted.";
}
