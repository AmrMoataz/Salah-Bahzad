using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// System-attributed terminal quiz operations (<see cref="IQuizLifecycleService"/>): the timer's auto-submit
/// (FR-PLAT-QZ-005) and the hub's forfeit-on-disconnect (FR-PLAT-QZ-004). Each wraps the work in the tenant's
/// <see cref="ISystemOperationContext"/> so — with no HTTP request — the global query filter scopes the load and
/// the audit interceptor writes the lifecycle row attributed to System. Runs in a transaction so the attempt
/// write commits before the post-commit grade event drives the attendance scorer (mirrors the answer→grade path).
/// </summary>
internal sealed class QuizLifecycleService(
    IAppDbContext db,
    ISystemOperationContext systemOperation,
    IQuizTimerScheduler timer,
    TimeProvider clock,
    ILogger<QuizLifecycleService> logger)
    : IQuizLifecycleService
{
    public async Task<bool> TimeOutAttemptAsync(
        Guid quizId, Guid attemptId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var scope = systemOperation.Begin(tenantId);

        return await db.ExecuteInTransactionAsync(async () =>
        {
            // Owned attempts load with the quiz; the system tenant scope makes the filter pass.
            var quiz = await db.UserQuizzes.FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken);
            var attempt = quiz?.TimeOutAttempt(attemptId, clock.GetUtcNow());
            if (attempt is null)
                return false; // not found, or a submit already raced the timer — idempotent no-op

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Quiz attempt {AttemptId} auto-submitted (TimedOut) by the authoritative timer.", attemptId);
            return true;
        }, cancellationToken);
    }

    public async Task<bool> ForfeitActiveAttemptAsync(
        Guid quizId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var scope = systemOperation.Begin(tenantId);

        var forfeitedAttemptId = await db.ExecuteInTransactionAsync<Guid?>(async () =>
        {
            var quiz = await db.UserQuizzes.FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken);
            var attempt = quiz?.ForfeitActiveAttempt(clock.GetUtcNow());
            if (attempt is null)
                return null; // no active attempt — a disconnect after a clean submit is a no-op

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Quiz attempt {AttemptId} forfeited (single-sitting connection lost).", attempt.Id);
            return attempt.Id;
        }, cancellationToken);

        if (forfeitedAttemptId is Guid id)
            timer.CancelAutoSubmit(id); // the pending auto-submit is now moot

        return forfeitedAttemptId is not null;
    }

    public async Task<Guid?> FindActiveAttemptQuizIdAsync(
        Guid studentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var scope = systemOperation.Begin(tenantId);

        return await db.UserQuizzes
            .AsNoTracking()
            .Where(q => q.StudentId == studentId
                && q.Attempts.Any(a => a.Status == Domain.Enums.QuizAttemptStatus.InProgress))
            .Select(q => (Guid?)q.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
