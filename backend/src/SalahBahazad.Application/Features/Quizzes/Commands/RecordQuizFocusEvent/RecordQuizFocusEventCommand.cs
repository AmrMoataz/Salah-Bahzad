using Mediator;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Quizzes.Commands.RecordQuizFocusEvent;

/// <summary>
/// Records a quiz-attempt focus event for the calling student (contract §A #5, FR-PLAT-QZ-006). High-volume →
/// the row lands in <c>assessment_events</c>, never the audit log. <b>Monitoring only — never auto-forfeits.</b>
/// The type must be <see cref="AssessmentEventType.FocusLost"/> or <see cref="AssessmentEventType.FocusReturned"/>.
/// </summary>
public sealed record RecordQuizFocusEventCommand(
    Guid AttemptId, AssessmentEventType Type, DateTimeOffset OccurredAtUtc, int? DurationMs) : IRequest<Unit>;
