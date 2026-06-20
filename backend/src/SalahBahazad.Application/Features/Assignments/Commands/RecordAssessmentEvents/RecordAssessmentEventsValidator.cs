using FluentValidation;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Assignments.Commands.RecordAssessmentEvents;

/// <summary>
/// The behaviour endpoint carries entered/left/navigated only — the <see cref="AssessmentEventType.Answered"/>
/// event is logged by the answer path (#2), never posted here (contract §A #3).
/// </summary>
internal sealed class RecordAssessmentEventsValidator : AbstractValidator<RecordAssessmentEventsCommand>
{
    public RecordAssessmentEventsValidator()
    {
        RuleFor(c => c.Type)
            .Must(t => t is AssessmentEventType.Entered or AssessmentEventType.Left or AssessmentEventType.Navigated)
            .WithMessage("Only Entered, Left or Navigated events may be posted here.");

        RuleFor(c => c.ElapsedMs)
            .GreaterThanOrEqualTo(0)
            .When(c => c.ElapsedMs.HasValue);

        RuleFor(c => c.QuestionOrder)
            .GreaterThan(0)
            .When(c => c.QuestionOrder.HasValue);
    }
}
