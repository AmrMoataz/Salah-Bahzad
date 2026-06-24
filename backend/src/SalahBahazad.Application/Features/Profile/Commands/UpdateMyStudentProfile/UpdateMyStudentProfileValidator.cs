using FluentValidation;

namespace SalahBahazad.Application.Features.Profile.Commands.UpdateMyStudentProfile;

/// <summary>
/// Shape validation for the self-service profile update (Student-Portal S6 §A.2), mirroring the register
/// validators' field rules. Pure (no DB access) — the city/region <i>existence</i> + region-belongs-to-city check
/// is a 400 enforced in the handler against the global-seeded reference set (§C.3), matching the codebase
/// convention that validators check shape and handlers check referential facts.
/// </summary>
internal sealed class UpdateMyStudentProfileValidator : AbstractValidator<UpdateMyStudentProfileCommand>
{
    public UpdateMyStudentProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Your name is required.")
            .MaximumLength(200);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("A phone number is required.")
            .MaximumLength(32);

        RuleFor(x => x.SchoolName)
            .NotEmpty().WithMessage("A school name is required.")
            .MaximumLength(200);

        RuleFor(x => x.ParentPhonePrimary)
            .NotEmpty().WithMessage("A parent/guardian phone number is required.")
            .MaximumLength(32);

        RuleFor(x => x.ParentPhoneSecondary).MaximumLength(32);

        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.RegionId).NotEmpty();
    }
}
