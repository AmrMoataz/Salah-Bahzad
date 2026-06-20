using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Application.Features.Enrollment.EventHandlers;

/// <summary>
/// Triggers the (Phase-4-stubbed) assignment + prerequisite-quiz snapshot generation when an enrollment is
/// granted or re-activated (FR-PLAT-ENR-005). Subscribing to <b>both</b> the created and extended events makes
/// a redeem (#12), an unlock (#9) and a re-enroll behave identically. Runs post-commit (events dispatch after
/// the transaction), so the snapshots are built only on durable enrollments.
/// </summary>
internal sealed class EnrollmentSideEffectsHandler(IEnrollmentSideEffects sideEffects)
    : INotificationHandler<EnrollmentCreatedEvent>, INotificationHandler<EnrollmentExtendedEvent>
{
    public async ValueTask Handle(EnrollmentCreatedEvent notification, CancellationToken cancellationToken)
        => await sideEffects.GenerateAssessmentsAsync(notification.EnrollmentId, cancellationToken);

    public async ValueTask Handle(EnrollmentExtendedEvent notification, CancellationToken cancellationToken)
        => await sideEffects.GenerateAssessmentsAsync(notification.EnrollmentId, cancellationToken);
}
