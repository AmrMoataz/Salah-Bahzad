using FluentValidation;

namespace SalahBahazad.Application.Features.Enrollment.Commands.UnlockSession;

internal sealed class UnlockSessionValidator : AbstractValidator<UnlockSessionCommand>
{
    public UnlockSessionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
    }
}
