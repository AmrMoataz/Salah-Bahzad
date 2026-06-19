using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.PublishSession;

/// <summary>Publishes a session to the catalogue (FR-PLAT-SES-008). Illegal state (already published, or a
/// quiz configured for more questions than are eligible) is a 409.</summary>
public sealed record PublishSessionCommand(Guid Id) : IRequest<SessionDetailDto>, ITransactionalRequest;
