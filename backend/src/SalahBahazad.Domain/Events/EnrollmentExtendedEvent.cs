using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when an existing (non-active) enrollment for the same student+session is re-activated in place —
/// counters reset, validity pushed forward — instead of creating a duplicate row (FR-PLAT-ENR-004). Shares
/// the side-effect handler with <see cref="EnrollmentCreatedEvent"/> so re-enroll behaves identically.
/// </summary>
public sealed record EnrollmentExtendedEvent(
    Guid EnrollmentId, Guid StudentId, Guid SessionId, EnrollmentMethod Method) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "EnrollmentExtended";
    public string AuditSummary => $"Enrollment extended via {Method}.";
}
