using FluentValidation;

namespace SalahBahazad.Application.Features.Questions.Commands.UpdateQuestion;

internal sealed class UpdateQuestionValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();

        // Body may be null on update when an image already exists; the handler enforces that rule.
        RuleFor(x => x.BodyLatex).MaximumLength(4000);

        RuleFor(x => x.Mark).GreaterThan(0);
        RuleFor(x => x.HintUrl).MaximumLength(1000);

        RuleFor(x => x.Options)
            .NotNull()
            .Must(o => o.Count >= 2).WithMessage("At least two options are required.")
            .Must(o => o.Count(opt => opt.IsCorrect) == 1).WithMessage("Exactly one option must be correct.");
        RuleForEach(x => x.Options)
            .ChildRules(opt => opt.RuleFor(o => o.Text).NotEmpty().MaximumLength(2000));
    }
}
