namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>
/// Thrown when an action conflicts with the current state, e.g. a duplicate email or a video not yet
/// transcoded (maps to HTTP 409). An optional <see cref="Reason"/> surfaces a machine-readable code.
/// </summary>
public sealed class ConflictException(string message, string? reason = null) : Exception(message), IProblemReason
{
    public string? Reason { get; } = reason;
}
