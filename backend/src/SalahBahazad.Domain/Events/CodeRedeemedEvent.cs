using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a student redeems a code (FR-PLAT-ENR-001). Attributed to the <b>student</b> actor by the
/// audit interceptor (the redeem request carries a Student-role JWT). The summary is serial-based; the
/// student/session names are resolved by the audit-feed projection, not the domain.
/// </summary>
public sealed record CodeRedeemedEvent(Guid CodeId, string Serial, Guid StudentId, Guid SessionId)
    : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "CodeRedeemed";
    public string AuditSummary => $"Code {Serial} redeemed.";
}
