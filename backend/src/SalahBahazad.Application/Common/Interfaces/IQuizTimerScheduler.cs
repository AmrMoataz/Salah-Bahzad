namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Schedules the <b>authoritative</b> server-side quiz auto-submit (FR-PLAT-QZ-005): a durable background job
/// that fires at the attempt's deadline and times it out if still in progress — surviving a dropped connection
/// or an API restart. Backed by Hangfire (backend-owned mechanism). Submit/forfeit cancels the pending job;
/// the job is idempotent regardless, so cancellation is a best-effort optimisation.
/// </summary>
public interface IQuizTimerScheduler
{
    /// <summary>Schedules the auto-submit of <paramref name="attemptId"/> at <paramref name="deadlineUtc"/>.</summary>
    void ScheduleAutoSubmit(Guid quizId, Guid attemptId, Guid tenantId, DateTimeOffset deadlineUtc);

    /// <summary>Best-effort cancellation of a pending auto-submit (the attempt ended early).</summary>
    void CancelAutoSubmit(Guid attemptId);
}
