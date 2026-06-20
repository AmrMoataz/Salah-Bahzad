using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a batch of redemption codes is minted (FR-PLAT-COD-001). The semantic summary lets the audit
/// interceptor record one readable entry for the whole batch — the individual minted codes are
/// <see cref="IAuditViaEventOnly"/> and so do not each add a row (contract §5, FR-PLAT-AUD-002).
/// </summary>
public sealed record CodeBatchGeneratedEvent(Guid BatchId, string Label, int Quantity, decimal Value)
    : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "CodeBatchGenerated";
    public string AuditSummary => $"Generated {Quantity} code(s) in batch {Label} at EGP {Value}.";
}
