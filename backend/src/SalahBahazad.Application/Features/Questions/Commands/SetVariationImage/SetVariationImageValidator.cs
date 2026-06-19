using FluentValidation;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.SetVariationImage;

internal sealed class SetVariationImageValidator : AbstractValidator<SetVariationImageCommand>
{
    public SetVariationImageValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.VariationId).NotEmpty();

        RuleFor(x => x.ContentType)
            .Must(ct => QuestionImageConstraints.AllowedContentTypes.Contains(ct))
            .WithMessage("The image must be a JPEG, PNG, or WebP.");

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The image file is required.")
            .LessThanOrEqualTo(QuestionImageConstraints.MaxBytes).WithMessage("The image must be 5 MB or smaller.");
    }
}
