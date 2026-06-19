using FluentValidation;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.UpdateSubject;

internal sealed class UpdateSubjectValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A subject name is required.")
            .MaximumLength(100);
    }
}
