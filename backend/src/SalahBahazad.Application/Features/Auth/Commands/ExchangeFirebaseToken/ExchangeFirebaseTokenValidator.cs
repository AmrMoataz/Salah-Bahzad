using FluentValidation;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeFirebaseToken;

internal sealed class ExchangeFirebaseTokenValidator : AbstractValidator<ExchangeFirebaseTokenCommand>
{
    public ExchangeFirebaseTokenValidator()
    {
        RuleFor(x => x.FirebaseIdToken)
            .NotEmpty()
            .WithMessage("Firebase ID token is required.");
    }
}
