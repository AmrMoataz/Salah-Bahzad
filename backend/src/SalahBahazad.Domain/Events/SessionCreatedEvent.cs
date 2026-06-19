using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a session is created (FR-PLAT-SES-009). Carries the semantic action so the audit
/// interceptor records a meaningful entry beyond its generic field-diff.
/// </summary>
public sealed record SessionCreatedEvent(Guid SessionId, string Title) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "SessionCreated";
    public string AuditSummary => $"Session created: {Title}";
}
