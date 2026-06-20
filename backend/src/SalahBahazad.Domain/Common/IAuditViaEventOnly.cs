namespace SalahBahazad.Domain.Common;

/// <summary>
/// Marks an entity whose audit trail is carried <b>solely</b> by its aggregate's semantic
/// <see cref="IAuditableDomainEvent"/>s — so the audit <c>SaveChangesInterceptor</c> must NOT emit a
/// generic per-row field-diff <c>AuditEntry</c> for it. This keeps bulk/child writes (a batch's minted
/// codes, an enrollment's per-video access counters, its payment + attendance shells) from flooding the
/// append-only log with one row each, so every Phase-4 lifecycle action leaves exactly one entry
/// (FR-PLAT-AUD-002). An entity so marked is still audited on any change that <i>does</i> buffer a
/// semantic event (e.g. a <c>Code</c> disable/enable/delete/redeem), because the interceptor falls back to
/// the event for those — only the eventless generic diffs are suppressed.
/// </summary>
public interface IAuditViaEventOnly;
