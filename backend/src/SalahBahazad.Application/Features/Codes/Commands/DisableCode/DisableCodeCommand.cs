using Mediator;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Commands.DisableCode;

/// <summary>Disables a code so it can no longer be redeemed (FR-PLAT-COD-004); 409 if it is already used.</summary>
public sealed record DisableCodeCommand(Guid Id) : IRequest<CodeListDto>;
