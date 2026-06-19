using FluentValidation;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.CreateSubject;

internal sealed class CreateSubjectValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A subject name is required.")
            .MaximumLength(100);
    }
}
