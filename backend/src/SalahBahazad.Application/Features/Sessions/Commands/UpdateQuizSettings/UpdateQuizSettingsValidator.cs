using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateQuizSettings;

/// <summary>Enforces the four quiz-knob ranges from the frozen contract (scrQuizSettings sliders).</summary>
internal sealed class UpdateQuizSettingsValidator : AbstractValidator<UpdateQuizSettingsCommand>
{
    public UpdateQuizSettingsValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TimeLimitMinutes).InclusiveBetween(5, 60);
        RuleFor(x => x.QuestionCount).InclusiveBetween(5, 30);
        RuleFor(x => x.AttemptCount).InclusiveBetween(1, 5);
        RuleFor(x => x.MinPassPercent).InclusiveBetween(40, 100);
    }
}
