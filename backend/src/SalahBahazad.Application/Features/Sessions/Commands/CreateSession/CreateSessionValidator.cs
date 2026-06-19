using FluentValidation;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Sessions.Commands.CreateSession;

internal sealed class CreateSessionValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidityDays).InclusiveBetween(0, Session.MaxValidityDays);
        RuleFor(x => x.GradeId).NotEmpty();
        RuleFor(x => x.SpecializationId).NotEmpty();
    }
}
