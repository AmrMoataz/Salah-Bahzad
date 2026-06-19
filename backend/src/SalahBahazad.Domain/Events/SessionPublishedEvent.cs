using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when a session is published to the catalogue (FR-PLAT-SES-008/009).</summary>
public sealed record SessionPublishedEvent(Guid SessionId) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "SessionPublished";
    public string AuditSummary => "Session published.";
}
