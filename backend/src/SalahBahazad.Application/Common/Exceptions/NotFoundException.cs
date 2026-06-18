namespace SalahBahazad.Application.Common.Exceptions;

/// <summary>Thrown when a requested entity does not exist (maps to HTTP 404).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.") { }
}
