namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>Thrown when an action conflicts with the current state, e.g. a duplicate email (maps to HTTP 409).</summary>
public sealed class ConflictException(string message) : Exception(message);
