using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when staff re-enable a previously disabled code (FR-PLAT-COD-004).</summary>
public sealed record CodeEnabledEvent(Guid CodeId, string Serial) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "CodeEnabled";
    public string AuditSummary => $"Code {Serial} enabled.";
}
