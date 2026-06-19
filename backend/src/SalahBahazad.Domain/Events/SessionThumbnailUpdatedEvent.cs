using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when a session's thumbnail is uploaded/replaced (FR-PLAT-SES-009, FR-ADM-SES-002).</summary>
public sealed record SessionThumbnailUpdatedEvent(Guid SessionId) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "SessionThumbnailUpdated";
    public string AuditSummary => "Session thumbnail updated.";
}
