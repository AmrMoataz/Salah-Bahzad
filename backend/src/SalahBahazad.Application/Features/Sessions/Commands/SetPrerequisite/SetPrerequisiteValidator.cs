using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Commands.SetPrerequisite;

internal sealed class SetPrerequisiteValidator : AbstractValidator<SetPrerequisiteCommand>
{
    public SetPrerequisiteValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
