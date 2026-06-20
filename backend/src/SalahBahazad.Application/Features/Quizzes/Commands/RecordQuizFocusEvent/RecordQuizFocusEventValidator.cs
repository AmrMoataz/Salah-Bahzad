using FluentValidation;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Quizzes.Commands.RecordQuizFocusEvent;

internal sealed class RecordQuizFocusEventValidator : AbstractValidator<RecordQuizFocusEventCommand>
{
    public RecordQuizFocusEventValidator()
    {
        // Only focus telemetry belongs on this path (FR-PLAT-QZ-006); answered/navigated are the assignment's.
        RuleFor(x => x.Type)
            .Must(t => t is AssessmentEventType.FocusLost or AssessmentEventType.FocusReturned)
            .WithMessage("Only FocusLost or FocusReturned may be recorded for a quiz attempt.");

        RuleFor(x => x.DurationMs).GreaterThanOrEqualTo(0).When(x => x.DurationMs.HasValue);
    }
}
