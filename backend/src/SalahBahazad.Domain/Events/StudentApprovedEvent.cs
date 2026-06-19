using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a pending student is approved (FR-ADM-STU-003). Carries the semantic action so a
/// handler can enrich the audit <c>Summary</c> beyond the interceptor's field-diff (FR-ADM-STU-010).
/// </summary>
public sealed record StudentApprovedEvent(Guid StudentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
