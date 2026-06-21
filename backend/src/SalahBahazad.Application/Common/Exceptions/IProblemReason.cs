namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>
/// Carries an optional machine-readable <c>reason</c> code that the global exception handler surfaces as a
/// ProblemDetails extension, alongside the human-readable message. Lets clients branch on a stable code
/// (e.g. the video gate's <c>no_views_remaining</c>) instead of parsing prose (FR-PLAT-VID-006).
/// </summary>
public interface IProblemReason
{
    string? Reason { get; }
}
