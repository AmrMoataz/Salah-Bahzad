namespace SalahBahazad.Domain.Common;

/// <summary>
/// A domain event that also carries a human-meaningful audit action and summary. The audit
/// <c>SaveChangesInterceptor</c> reads these (pre-commit, while the event is still buffered) to
/// replace its generic "Updated &lt;Entity&gt;" entry with a semantic one — in the <b>same</b>
/// hash-chained row. The interceptor's field-diff still records the "what" (Before/After JSON);
/// this supplies the "why" the diff cannot express, e.g. the reason on a reject or device-clear
/// (FR-PLAT-AUD-002, FR-ADM-STU-010).
/// </summary>
public interface IAuditableDomainEvent : IDomainEvent
{
    /// <summary>Semantic action verb stored as the audit entry's <c>Action</c> (e.g. "StudentRejected").</summary>
    string AuditAction { get; }

    /// <summary>Human-readable summary, may embed the reason (e.g. "Student rejected: duplicate account").</summary>
    string AuditSummary { get; }
}
