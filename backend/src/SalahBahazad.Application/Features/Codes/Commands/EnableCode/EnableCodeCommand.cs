using Mediator;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Commands.EnableCode;

/// <summary>Re-enables a disabled code (FR-PLAT-COD-004); 409 if it is already used.</summary>
public sealed record EnableCodeCommand(Guid Id) : IRequest<CodeListDto>;
