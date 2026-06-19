using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when a session's core authoring details change (FR-PLAT-SES-009, FR-ADM-SES-002).
/// Lets the audit interceptor record a meaningful entry instead of its generic "Updated Session".</summary>
public sealed record SessionDetailsUpdatedEvent(Guid SessionId, string Title) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "SessionUpdated";
    public string AuditSummary => $"Session details updated: {Title}";
}
