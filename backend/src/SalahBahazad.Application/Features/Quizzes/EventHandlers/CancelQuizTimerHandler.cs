using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Application.Features.Quizzes.EventHandlers;

/// <summary>
/// Cancels the pending auto-submit timer when the student submits an attempt (FR-PLAT-QZ-005). Best-effort: the
/// Hangfire job is idempotent and no-ops on an already-terminal attempt, so a missed cancellation is harmless.
/// Runs post-commit (the submitted event dispatches after the submit transaction).
/// </summary>
internal sealed class CancelQuizTimerHandler(IQuizTimerScheduler timer)
    : INotificationHandler<QuizAttemptSubmittedEvent>
{
    public ValueTask Handle(QuizAttemptSubmittedEvent notification, CancellationToken cancellationToken)
    {
        timer.CancelAutoSubmit(notification.AttemptId);
        return ValueTask.CompletedTask;
    }
}
