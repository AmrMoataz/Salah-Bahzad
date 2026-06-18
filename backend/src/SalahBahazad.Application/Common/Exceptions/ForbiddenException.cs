namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated actor is not permitted to perform an action — e.g. assigning a role
/// higher than their own (FR-PLAT-ROLE-002). Maps to HTTP 403.
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);
