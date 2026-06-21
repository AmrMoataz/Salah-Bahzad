namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>
/// Thrown when a resource that once existed is no longer available — e.g. a one-time playback handoff code
/// that was already consumed or has expired (FR-PLAT-VID-005). Maps to HTTP 410; an optional
/// <see cref="Reason"/> surfaces a machine-readable code in the ProblemDetails.
/// </summary>
public sealed class GoneException(string message, string? reason = null) : Exception(message), IProblemReason
{
    public string? Reason { get; } = reason;
}
