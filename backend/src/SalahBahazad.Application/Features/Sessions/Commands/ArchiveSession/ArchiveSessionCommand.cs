using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.ArchiveSession;

/// <summary>Archives/retires a session (FR-PLAT-SES-001). Already-archived is a 409.</summary>
public sealed record ArchiveSessionCommand(Guid Id) : IRequest<SessionDetailDto>, ITransactionalRequest;
