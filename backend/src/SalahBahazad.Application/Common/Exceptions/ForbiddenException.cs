namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated actor is not permitted to perform an action — e.g. assigning a role
/// higher than their own (FR-PLAT-ROLE-002), or a video playback gate failure (FR-PLAT-VID-006). Maps to
/// HTTP 403; an optional <see cref="Reason"/> surfaces a machine-readable code in the ProblemDetails.
/// </summary>
public sealed class ForbiddenException(string message, string? reason = null) : Exception(message), IProblemReason
{
    public string? Reason { get; } = reason;
}
