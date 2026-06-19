using FluentValidation;

namespace SalahBahazad.Application.Features.Profile.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Your name is required.")
            .MaximumLength(200);
    }
}
