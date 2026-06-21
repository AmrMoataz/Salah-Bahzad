using FluentValidation;
using SalahBahazad.Application.Features.Sessions.Commands.AddSessionVideo;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateSessionVideo;

internal sealed class UpdateSessionVideoValidator : AbstractValidator<UpdateSessionVideoCommand>
{
    public UpdateSessionVideoValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.VideoId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccessCount).GreaterThanOrEqualTo(0);

        // Validate the file only when a replacement is supplied (metadata-only edits send no file).
        When(x => x.HasNewSource, () =>
        {
            RuleFor(x => x.ContentType!)
                .Must(ct => AddSessionVideoCommand.AllowedContentTypes.Contains(ct))
                .WithMessage("The video must be MP4, MOV, or WebM.");

            RuleFor(x => x.Length)
                .NotNull()
                .GreaterThan(0)
                .LessThanOrEqualTo(AddSessionVideoCommand.MaxBytes)
                .WithMessage("The video must be 2 GB or smaller.");
        });
    }
}
