using FluentValidation;

namespace SalahBahazad.Application.Features.App.Queries.GetAppVersionStatus;

internal sealed class GetAppVersionStatusValidator : AbstractValidator<GetAppVersionStatusQuery>
{
    private static readonly HashSet<string> KnownPlatforms =
        new(StringComparer.OrdinalIgnoreCase) { "android", "ios", "windows", "macos" };

    public GetAppVersionStatusValidator()
    {
        RuleFor(q => q.Platform)
            .NotEmpty().WithMessage("platform is required.")
            .Must(p => KnownPlatforms.Contains(p))
            .WithMessage("platform must be one of: android | ios | windows | macos.");

        RuleFor(q => q.Version)
            .NotEmpty().WithMessage("version is required.")
            .Must(v => Version.TryParse(v, out _))
            .WithMessage("version must be a valid version string (e.g. 1.0.0).");
    }
}
