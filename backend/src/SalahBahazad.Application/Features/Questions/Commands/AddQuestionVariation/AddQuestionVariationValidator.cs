using FluentValidation;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.AddQuestionVariation;

internal sealed class AddQuestionVariationValidator : AbstractValidator<AddQuestionVariationCommand>
{
    public AddQuestionVariationValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();

        // A LaTeX body is required only when no image is supplied — an image-only variation is valid (FR-PLAT-QB-002).
        RuleFor(x => x.BodyLatex)
            .NotEmpty().When(x => string.IsNullOrWhiteSpace(x.ImageBase64))
                .WithMessage("LaTeX text is required when there is no image.")
            .MaximumLength(4000);

        When(x => !string.IsNullOrWhiteSpace(x.ImageBase64), () =>
        {
            RuleFor(x => x.ImageContentType)
                .Must(ct => ct is not null && QuestionImageConstraints.AllowedContentTypes.Contains(ct))
                .WithMessage("The image must be a JPEG, PNG, or WebP.");
            RuleFor(x => x.ImageBase64!)
                .Must(IsValidBase64).WithMessage("The image upload is invalid.")
                .Must(b64 => DecodedByteCount(b64) <= QuestionImageConstraints.MaxBytes)
                    .WithMessage("The image must be 5 MB or smaller.");
        });

        RuleFor(x => x.Options)
            .NotNull()
            .Must(o => o.Count >= 2).WithMessage("At least two options are required.")
            .Must(o => o.Count(opt => opt.IsCorrect) == 1).WithMessage("Exactly one option must be correct.");
        RuleForEach(x => x.Options)
            .ChildRules(opt => opt.RuleFor(o => o.Text).NotEmpty().MaximumLength(2000));
    }

    private static bool IsValidBase64(string value)
    {
        Span<byte> buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out _);
    }

    /// <summary>Decoded byte length of a base64 string without allocating the decoded buffer.</summary>
    private static long DecodedByteCount(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var padding = value.EndsWith("==", StringComparison.Ordinal) ? 2 : value.EndsWith('=') ? 1 : 0;
        return (long)value.Length * 3 / 4 - padding;
    }
}
