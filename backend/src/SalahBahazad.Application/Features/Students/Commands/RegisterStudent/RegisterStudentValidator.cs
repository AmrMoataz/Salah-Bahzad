using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Commands.RegisterStudent;

internal sealed class RegisterStudentValidator : AbstractValidator<RegisterStudentCommand>
{
    public RegisterStudentValidator()
    {
        RuleFor(x => x.FirebaseIdToken).NotEmpty();
        RuleFor(x => x.TenantSlug).NotEmpty();

        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ParentPhonePrimary)
            .NotEmpty().WithMessage("A parent/guardian phone number is required.")
            .MaximumLength(32);
        RuleFor(x => x.ParentPhoneSecondary).MaximumLength(32);

        RuleFor(x => x.GradeId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.RegionId).NotEmpty();
        RuleFor(x => x.SchoolName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TermsVersion).NotEmpty().MaximumLength(50);

        RuleFor(x => x.IdImageContentType)
            .Must(ct => RegisterStudentCommand.AllowedImageContentTypes.Contains(ct))
            .WithMessage("The ID image must be a JPEG, PNG, or WebP.");

        RuleFor(x => x.IdImageLength)
            .GreaterThan(0).WithMessage("The ID image is required.")
            .LessThanOrEqualTo(RegisterStudentCommand.MaxImageBytes)
            .WithMessage("The ID image must be 5 MB or smaller.");
    }
}
