using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when staff disable a code (FR-PLAT-COD-004); the code becomes non-redeemable but is retained.</summary>
public sealed record CodeDisabledEvent(Guid CodeId, string Serial) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "CodeDisabled";
    public string AuditSummary => $"Code {Serial} disabled.";
}
