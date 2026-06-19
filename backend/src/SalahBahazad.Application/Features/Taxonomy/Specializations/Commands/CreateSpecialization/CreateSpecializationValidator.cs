using FluentValidation;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.CreateSpecialization;

internal sealed class CreateSpecializationValidator : AbstractValidator<CreateSpecializationCommand>
{
    public CreateSpecializationValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage("A parent subject is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A specialization name is required.")
            .MaximumLength(100);
    }
}
