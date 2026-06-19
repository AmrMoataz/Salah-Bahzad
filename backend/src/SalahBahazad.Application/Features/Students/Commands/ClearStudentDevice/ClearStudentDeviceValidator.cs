using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Commands.ClearStudentDevice;

internal sealed class ClearStudentDeviceValidator : AbstractValidator<ClearStudentDeviceCommand>
{
    public ClearStudentDeviceValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required to clear a device.")
            .MaximumLength(1000);
    }
}
