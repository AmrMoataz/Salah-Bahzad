using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Commands.RejectStudent;

internal sealed class RejectStudentValidator : AbstractValidator<RejectStudentCommand>
{
    public RejectStudentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A rejection reason is required.")
            .MaximumLength(1000);
    }
}
