namespace SalahBahazad.Application.Common;

/// <summary>
/// Per-platform minimum and latest app version floors. Hot-reloadable via <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/>.
/// Raises or lowers the minimum floor without a redeploy — edit <c>appsettings.{Environment}.json</c> or an
/// environment variable override. See contract §F (NFR-APP-UPD-002).
/// </summary>
public sealed class AppVersionsOptions
{
    public const string SectionName = "AppVersions";

    /// <summary>Keyed by canonical lowercase platform name: <c>android | ios | windows | macos</c>.</summary>
    public Dictionary<string, PlatformVersionOptions> Platforms { get; init; } = new();
}

/// <summary>Version config for a single platform.</summary>
public sealed class PlatformVersionOptions
{
    public string MinVersion { get; init; } = "1.0.0";
    public string LatestVersion { get; init; } = "1.0.0";

    /// <summary>Store or download URL surfaced in the <c>426 outdated_app</c> ProblemDetails so the app can deep-link the student to the update.</summary>
    public string StoreUrl { get; init; } = string.Empty;
}
