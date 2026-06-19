using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Commands.AddSessionMaterial;

internal sealed class AddSessionMaterialValidator : AbstractValidator<AddSessionMaterialCommand>
{
    public AddSessionMaterialValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);

        RuleFor(x => x.ContentType)
            .Must(ct => AddSessionMaterialCommand.AllowedContentTypes.Contains(ct))
            .WithMessage("Materials must be a PDF, CSV, PNG, or JPEG.");

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The material file is required.")
            .LessThanOrEqualTo(AddSessionMaterialCommand.MaxBytes).WithMessage("The material must be 25 MB or smaller.");
    }
}
