using FluentValidation;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentFirebaseToken;

internal sealed class ExchangeStudentFirebaseTokenValidator : AbstractValidator<ExchangeStudentFirebaseTokenCommand>
{
    public ExchangeStudentFirebaseTokenValidator()
    {
        RuleFor(x => x.FirebaseIdToken)
            .NotEmpty()
            .WithMessage("Firebase ID token is required.");
    }
}
