using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when staff refund an enrollment (FR-PLAT-ENR-008). When the enrollment was granted by a code, that
/// code is returned (<c>Used → Active</c>) and its <paramref name="ReturnedCodeSerial"/> is named in the summary.
/// </summary>
public sealed record EnrollmentRefundedEvent(
    Guid EnrollmentId, Guid StudentId, Guid SessionId, string? ReturnedCodeSerial) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "EnrollmentRefunded";

    public string AuditSummary => ReturnedCodeSerial is null
        ? "Enrollment refunded."
        : $"Enrollment refunded; code {ReturnedCodeSerial} returned.";
}
