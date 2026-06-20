namespace SalahBahazad.Domain.Common;

/// <summary>
/// An <see cref="IAuditableDomainEvent"/> whose audit entry must be attributed to the <b>System</b> actor
/// (FR-PLAT-AUD-005), not the principal of the triggering request. Used for platform-performed actions that
/// happen to run inside a user's request — e.g. assignment auto-generation on enrol or auto-grading on the
/// student's final answer. The audit <c>SaveChangesInterceptor</c> reads this marker and overrides that
/// single row's actor (id/type/role) to System, while the field-diff and hash chain are unaffected.
/// </summary>
public interface ISystemActorAuditEvent : IAuditableDomainEvent;
