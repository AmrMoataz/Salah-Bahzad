using FluentValidation;

namespace SalahBahazad.Application.Features.Questions.Commands.UpdateQuestionVariation;

internal sealed class UpdateQuestionVariationValidator : AbstractValidator<UpdateQuestionVariationCommand>
{
    public UpdateQuestionVariationValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.VariationId).NotEmpty();

        RuleFor(x => x.BodyLatex).MaximumLength(4000);

        RuleFor(x => x.Options)
            .NotNull()
            .Must(o => o.Count >= 2).WithMessage("At least two options are required.")
            .Must(o => o.Count(opt => opt.IsCorrect) == 1).WithMessage("Exactly one option must be correct.");
        RuleForEach(x => x.Options)
            .ChildRules(opt => opt.RuleFor(o => o.Text).NotEmpty().MaximumLength(2000));
    }
}
