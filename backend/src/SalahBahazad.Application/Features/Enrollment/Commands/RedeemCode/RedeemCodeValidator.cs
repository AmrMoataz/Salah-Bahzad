using FluentValidation;

namespace SalahBahazad.Application.Features.Enrollment.Commands.RedeemCode;

internal sealed class RedeemCodeValidator : AbstractValidator<RedeemCodeCommand>
{
    public RedeemCodeValidator()
    {
        RuleFor(x => x.Serial).NotEmpty().MaximumLength(20);
    }
}
