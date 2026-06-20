using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a brand-new enrollment is granted (FR-PLAT-ENR-005). Its <see cref="Method"/> distinguishes a
/// student redeem (#12) from a staff unlock (#9). Its handler triggers the (Phase-4-stubbed) assignment +
/// prerequisite-quiz snapshot generation (FR-PLAT-ASG-001, FR-PLAT-QZ-001).
/// </summary>
public sealed record EnrollmentCreatedEvent(
    Guid EnrollmentId, Guid StudentId, Guid SessionId, EnrollmentMethod Method) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "EnrollmentCreated";
    public string AuditSummary => $"Enrollment created via {Method}.";
}
