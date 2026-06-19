using FluentValidation;

namespace SalahBahazad.Application.Features.Questions.Commands.AddQuestionVariation;

internal sealed class AddQuestionVariationValidator : AbstractValidator<AddQuestionVariationCommand>
{
    public AddQuestionVariationValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();

        RuleFor(x => x.BodyLatex)
            .NotEmpty().WithMessage("LaTeX text is required (an image can be added afterwards).")
            .MaximumLength(4000);

        RuleFor(x => x.Options)
            .NotNull()
            .Must(o => o.Count >= 2).WithMessage("At least two options are required.")
            .Must(o => o.Count(opt => opt.IsCorrect) == 1).WithMessage("Exactly one option must be correct.");
        RuleForEach(x => x.Options)
            .ChildRules(opt => opt.RuleFor(o => o.Text).NotEmpty().MaximumLength(2000));
    }
}
