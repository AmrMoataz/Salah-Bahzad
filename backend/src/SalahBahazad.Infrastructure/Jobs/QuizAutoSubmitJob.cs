using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Jobs;

/// <summary>
/// The Hangfire job that enforces the authoritative quiz deadline (FR-PLAT-QZ-005): scheduled at an attempt's
/// <c>DeadlineUtc</c> when it starts, it auto-submits the attempt as <c>TimedOut</c> if still in progress.
/// Idempotent — a no-op when the attempt has already been submitted/forfeited — so it is safe to run even after
/// the attempt ended (the cancellation on submit/forfeit is only an optimisation). Public so Hangfire's
/// DI-backed activator can resolve and invoke it.
/// </summary>
public sealed class QuizAutoSubmitJob(IQuizLifecycleService lifecycle)
{
    public Task RunAsync(Guid quizId, Guid attemptId, Guid tenantId)
        => lifecycle.TimeOutAttemptAsync(quizId, attemptId, tenantId, CancellationToken.None);
}
