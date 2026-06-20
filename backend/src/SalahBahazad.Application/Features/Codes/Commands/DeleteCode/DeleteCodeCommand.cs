using Mediator;

namespace SalahBahazad.Application.Features.Codes.Commands.DeleteCode;

/// <summary>Soft-deletes a code (FR-PLAT-COD-004); 409 if it is already used.</summary>
public sealed record DeleteCodeCommand(Guid Id) : IRequest<Unit>;
