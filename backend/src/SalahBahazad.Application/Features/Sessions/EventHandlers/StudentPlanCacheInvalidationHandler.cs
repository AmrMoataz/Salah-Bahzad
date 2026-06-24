using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Application.Features.Sessions.EventHandlers;

/// <summary>
/// Drops the caller's cached weekly plan (contract §D) whenever a domain event changes the state the plan derives:
/// an enrollment created/extended/refunded (a session enters/leaves the enrolled set), a quiz graded (the gate may
/// flip and videos unlock), or an assignment graded (a step completes / the focus rolls forward). Each event
/// carries the <c>StudentId</c>, so the seam (<see cref="IStudentPlanCache.InvalidateAsync"/>) needs no lookup.
/// Runs post-commit (events dispatch after the transaction), so the plan recomputes off committed state on the
/// next read — no TTL wait. The two write sites that raise <b>no</b> event (the 5C video-playback gate and a
/// non-final assignment answer) call the seam inline instead.
/// </summary>
internal sealed class StudentPlanCacheInvalidationHandler(IStudentPlanCache planCache)
    : INotificationHandler<EnrollmentCreatedEvent>,
        INotificationHandler<EnrollmentExtendedEvent>,
        INotificationHandler<EnrollmentRefundedEvent>,
        INotificationHandler<QuizGradedEvent>,
        INotificationHandler<AssignmentGradedEvent>
{
    public ValueTask Handle(EnrollmentCreatedEvent notification, CancellationToken cancellationToken)
        => planCache.InvalidateAsync(notification.StudentId, cancellationToken);

    public ValueTask Handle(EnrollmentExtendedEvent notification, CancellationToken cancellationToken)
        => planCache.InvalidateAsync(notification.StudentId, cancellationToken);

    public ValueTask Handle(EnrollmentRefundedEvent notification, CancellationToken cancellationToken)
        => planCache.InvalidateAsync(notification.StudentId, cancellationToken);

    public ValueTask Handle(QuizGradedEvent notification, CancellationToken cancellationToken)
        => planCache.InvalidateAsync(notification.StudentId, cancellationToken);

    public ValueTask Handle(AssignmentGradedEvent notification, CancellationToken cancellationToken)
        => planCache.InvalidateAsync(notification.StudentId, cancellationToken);
}
