using Mediator;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetSessionById;

/// <summary>Full session detail for the detail/edit screens (FR-ADM-SES-007).</summary>
public sealed record GetSessionByIdQuery(Guid Id) : IRequest<SessionDetailDto>;
