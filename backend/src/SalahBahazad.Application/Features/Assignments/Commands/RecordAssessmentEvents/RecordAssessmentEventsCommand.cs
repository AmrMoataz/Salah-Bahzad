using Mediator;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Assignments.Commands.RecordAssessmentEvents;

/// <summary>
/// Appends an in-assessment behaviour event for the calling student and accrues elapsed time (contract §A #3,
/// FR-PLAT-ASG-004/005). High-volume → the row lands in <c>assessment_events</c>, never the audit log. The
/// answered-question event is logged by the answer path (#2); this path carries entered/left/navigated. A single
/// atomic save (event insert + time accrual) — no domain event to dispatch, so not transactional.
/// </summary>
public sealed record RecordAssessmentEventsCommand(
    Guid AssignmentId,
    AssessmentEventType Type,
    int? QuestionOrder,
    DateTimeOffset OccurredAtUtc,
    int? ElapsedMs) : IRequest<Unit>;
