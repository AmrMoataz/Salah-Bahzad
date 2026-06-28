namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>
/// Thrown when the calling app is below the configured minimum version for playback (NFR-APP-UPD-002).
/// The message carries the store URL so the global exception handler surfaces it as <c>ProblemDetails.Detail</c>.
/// Maps to HTTP 426 Upgrade Required; <see cref="Reason"/> is always <c>outdated_app</c>.
/// </summary>
public sealed class UpgradeRequiredException(string storeUrl)
    : Exception(storeUrl), IProblemReason
{
    public string? Reason => "outdated_app";
}
