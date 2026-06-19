using FluentValidation;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.CreateGrade;

internal sealed class CreateGradeValidator : AbstractValidator<CreateGradeCommand>
{
    public CreateGradeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A grade name is required.")
            .MaximumLength(100);
    }
}
