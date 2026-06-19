using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListSessionActivity;

/// <summary>Paged audit history for a session — every audit entry keyed to it, for the detail Activity
/// tab (FR-PLAT-SES-009).</summary>
public sealed record ListSessionActivityQuery(
    Guid SessionId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<SessionActivityDto>>;
