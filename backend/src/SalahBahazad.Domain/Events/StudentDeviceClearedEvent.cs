using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when staff clear a student's bound device (FR-PLAT-DEV-004). The mandatory
/// <paramref name="Reason"/> is carried into the audit <c>Summary</c>; the interceptor's field-diff
/// records the state change but not the "why".
/// </summary>
public sealed record StudentDeviceClearedEvent(Guid StudentId, Guid DeviceId, string Reason) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "StudentDeviceCleared";
    public string AuditSummary => $"Bound device cleared: {Reason}";
}
