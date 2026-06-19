using FluentValidation;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.UpdateSpecialization;

internal sealed class UpdateSpecializationValidator : AbstractValidator<UpdateSpecializationCommand>
{
    public UpdateSpecializationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage("A parent subject is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A specialization name is required.")
            .MaximumLength(100);
    }
}
