using FluentValidation;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Staff.Commands.CreateStaff;

internal sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(320);

        RuleFor(x => x.Role)
            .IsInEnum().NotEqual(StaffRole.None)
            .WithMessage("A staff role (Assistant or Teacher) is required.");
    }
}
