namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Performs the <b>System-attributed</b> terminal quiz operations that run outside a student request — the
/// Hangfire timer's auto-submit (FR-PLAT-QZ-005) and the hub's forfeit-on-disconnect (FR-PLAT-QZ-004). Each
/// establishes the tenant's <see cref="ISystemOperationContext"/> so the global query filter scopes correctly
/// and the audit row is written and attributed to System (FR-PLAT-AUD-005). Both are idempotent: a no-op if the
/// attempt has already terminated (a submit can race the timer/disconnect).
/// </summary>
public interface IQuizLifecycleService
{
    /// <summary>Auto-submits the attempt at its deadline as <c>TimedOut</c>. Returns false if already terminal.</summary>
    Task<bool> TimeOutAttemptAsync(
        Guid quizId, Guid attemptId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Forfeits the quiz's active attempt (score 0). Returns false if none is active.</summary>
    Task<bool> ForfeitActiveAttemptAsync(Guid quizId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>The id of the quiz the student currently has an in-progress attempt in, or null — used by the
    /// hub to map a connection to its attempt on connect (FR-PLAT-QZ-004).</summary>
    Task<Guid?> FindActiveAttemptQuizIdAsync(
        Guid studentId, Guid tenantId, CancellationToken cancellationToken = default);
}
