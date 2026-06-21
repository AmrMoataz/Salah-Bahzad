using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Commands.AddSessionVideo;

internal sealed class AddSessionVideoValidator : AbstractValidator<AddSessionVideoCommand>
{
    public AddSessionVideoValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccessCount).GreaterThanOrEqualTo(0);

        RuleFor(x => x.ContentType)
            .Must(ct => AddSessionVideoCommand.AllowedContentTypes.Contains(ct))
            .WithMessage("The video must be MP4, MOV, mkv, or WebM.");

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The video file is required.")
            .LessThanOrEqualTo(AddSessionVideoCommand.MaxBytes).WithMessage("The video must be 2 GB or smaller.");
    }
}
