using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Commands.SetSessionThumbnail;

internal sealed class SetSessionThumbnailValidator : AbstractValidator<SetSessionThumbnailCommand>
{
    public SetSessionThumbnailValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.ContentType)
            .Must(ct => SetSessionThumbnailCommand.AllowedContentTypes.Contains(ct))
            .WithMessage("The thumbnail must be a JPEG, PNG, or WebP.");

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The thumbnail file is required.")
            .LessThanOrEqualTo(SetSessionThumbnailCommand.MaxBytes)
            .WithMessage("The thumbnail must be 5 MB or smaller.");
    }
}
