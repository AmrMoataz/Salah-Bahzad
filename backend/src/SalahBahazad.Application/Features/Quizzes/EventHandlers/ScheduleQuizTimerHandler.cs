using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Application.Features.Quizzes.EventHandlers;

/// <summary>
/// Schedules the authoritative auto-submit timer when an attempt starts (FR-PLAT-QZ-005). Runs post-commit (the
/// started event dispatches after the start transaction), so the durable Hangfire job is only scheduled once the
/// attempt is persisted.
/// </summary>
internal sealed class ScheduleQuizTimerHandler(IQuizTimerScheduler timer)
    : INotificationHandler<QuizAttemptStartedEvent>
{
    public ValueTask Handle(QuizAttemptStartedEvent notification, CancellationToken cancellationToken)
    {
        timer.ScheduleAutoSubmit(
            notification.UserQuizId, notification.AttemptId, notification.TenantId, notification.DeadlineUtc);
        return ValueTask.CompletedTask;
    }
}
