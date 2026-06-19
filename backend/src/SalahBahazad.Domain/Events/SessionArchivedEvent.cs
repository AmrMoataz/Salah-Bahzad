using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when a session is archived/retired (FR-PLAT-SES-009).</summary>
public sealed record SessionArchivedEvent(Guid SessionId) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "SessionArchived";
    public string AuditSummary => "Session archived.";
}
