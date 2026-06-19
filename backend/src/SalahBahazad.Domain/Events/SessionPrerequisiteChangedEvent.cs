using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when a session's enrollment prerequisite is set or cleared (FR-PLAT-SES-009,
/// FR-ADM-SES-005).</summary>
public sealed record SessionPrerequisiteChangedEvent(Guid SessionId, bool Cleared) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "SessionPrerequisiteChanged";
    public string AuditSummary => Cleared ? "Prerequisite cleared." : "Prerequisite set.";
}
