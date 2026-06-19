using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a deactivated student account is re-activated by staff (FR-ADM-STU-006). Lets a
/// handler record a semantic audit <c>Summary</c> for the lifecycle transition (FR-ADM-STU-010).
/// </summary>
public sealed record StudentReactivatedEvent(Guid StudentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
