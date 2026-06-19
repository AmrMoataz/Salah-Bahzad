using FluentValidation;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.UpdateGrade;

internal sealed class UpdateGradeValidator : AbstractValidator<UpdateGradeCommand>
{
    public UpdateGradeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A grade name is required.")
            .MaximumLength(100);
    }
}
