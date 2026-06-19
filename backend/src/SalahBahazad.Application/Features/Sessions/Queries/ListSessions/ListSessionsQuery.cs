using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListSessions;

/// <summary>
/// Lists sessions for the admin catalogue, filtered by grade/subject/status with free-text title search,
/// paginated, with per-row question/video/enrolled stats (FR-ADM-SES-001). Subject is matched via the
/// session's specialization (FR-PLAT-TAX-002).
/// </summary>
public sealed record ListSessionsQuery(
    string? Search = null,
    Guid? GradeId = null,
    Guid? SubjectId = null,
    SessionStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<SessionListDto>>;
