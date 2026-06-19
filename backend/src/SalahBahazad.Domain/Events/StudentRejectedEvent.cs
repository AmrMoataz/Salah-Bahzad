using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a pending student is rejected (FR-ADM-STU-004). The mandatory <paramref name="Reason"/>
/// is carried into the audit <c>Summary</c> and surfaced to the student (FR-PLAT-NOT-001) — the
/// interceptor's automatic field-diff cannot express "why", so this semantic event supplies it.
/// </summary>
public sealed record StudentRejectedEvent(Guid StudentId, string Reason) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
