using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Infrastructure.Jobs;
using StackExchange.Redis;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// <see cref="IQuizTimerScheduler"/> over Hangfire: schedules the durable <see cref="QuizAutoSubmitJob"/> at the
/// attempt's deadline (persisted in PostgreSQL, so it survives an API restart — FR-PLAT-QZ-005). The job id is
/// recorded in Redis keyed by attempt so submit/forfeit can cancel the pending job; cancellation is best-effort
/// (the job is idempotent regardless). Redis is resolved optionally so a Redis-less environment still schedules.
/// </summary>
internal sealed class HangfireQuizTimerScheduler(
    IBackgroundJobClient backgroundJobs,
    IServiceProvider serviceProvider,
    ILogger<HangfireQuizTimerScheduler> logger)
    : IQuizTimerScheduler
{
    private static string RedisKey(Guid attemptId) => $"quiz:timer:{attemptId:N}";

    public void ScheduleAutoSubmit(Guid quizId, Guid attemptId, Guid tenantId, DateTimeOffset deadlineUtc)
    {
        var jobId = backgroundJobs.Schedule<QuizAutoSubmitJob>(
            job => job.RunAsync(quizId, attemptId, tenantId), deadlineUtc);

        var db = TryGetRedis();
        if (db is not null)
        {
            // Keep the mapping a little past the deadline so a late submit can still cancel.
            var ttl = deadlineUtc - DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5);
            db.StringSet(RedisKey(attemptId), jobId, ttl > TimeSpan.Zero ? ttl : TimeSpan.FromMinutes(5));
        }
    }

    public void CancelAutoSubmit(Guid attemptId)
    {
        var db = TryGetRedis();
        if (db is null)
            return; // no mapping store — the idempotent job will simply no-op at the deadline

        var jobId = db.StringGet(RedisKey(attemptId));
        if (jobId.HasValue)
        {
            backgroundJobs.Delete(jobId!);
            db.KeyDelete(RedisKey(attemptId));
        }
    }

    private IDatabase? TryGetRedis()
    {
        try
        {
            return serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable for quiz timer cancellation; relying on idempotent job.");
            return null;
        }
    }
}
