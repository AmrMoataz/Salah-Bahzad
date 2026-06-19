using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Commands.UpdateStudentContact;

internal sealed class UpdateStudentContactValidator : AbstractValidator<UpdateStudentContactCommand>
{
    public UpdateStudentContactValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.GradeId).NotEmpty().WithMessage("A grade is required.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("A phone number is required.")
            .MaximumLength(32);

        RuleFor(x => x.ParentPhonePrimary)
            .NotEmpty().WithMessage("A parent/guardian phone number is required.")
            .MaximumLength(32);

        RuleFor(x => x.ParentPhoneSecondary)
            .MaximumLength(32);
    }
}
