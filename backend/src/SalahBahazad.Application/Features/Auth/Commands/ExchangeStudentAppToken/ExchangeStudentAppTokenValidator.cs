using FluentValidation;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentAppToken;

internal sealed class ExchangeStudentAppTokenValidator : AbstractValidator<ExchangeStudentAppTokenCommand>
{
    public ExchangeStudentAppTokenValidator()
    {
        RuleFor(x => x.FirebaseIdToken)
            .NotEmpty()
            .WithMessage("Firebase ID token is required.");
    }
}
