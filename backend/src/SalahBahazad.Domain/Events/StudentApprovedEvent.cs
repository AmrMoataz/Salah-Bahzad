using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a pending student is approved (FR-ADM-STU-003). Carries the semantic action so the
/// audit interceptor records a meaningful entry beyond its field-diff (FR-ADM-STU-010).
/// </summary>
public sealed record StudentApprovedEvent(Guid StudentId) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "StudentApproved";
    public string AuditSummary => "Student approved.";
}
