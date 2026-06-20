using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>Raised when a code is soft-deleted (FR-PLAT-COD-004); it drops out of the register via the query filter.</summary>
public sealed record CodeDeletedEvent(Guid CodeId, string Serial) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "CodeDeleted";
    public string AuditSummary => $"Code {Serial} deleted.";
}
