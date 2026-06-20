using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Attendance.DTOs;

namespace SalahBahazad.Application.Features.Attendance.Queries.ListSessionAttendance;

/// <summary>
/// A session's attendance matrix (contract §B #4, FR-ADM-ATT-001): one row per <c>Active</c>/<c>Expired</c>
/// enrollment, joined to the student's <c>Attendance</c> metrics and the session's video total, paged.
/// </summary>
public sealed record ListSessionAttendanceQuery(Guid SessionId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<SessionAttendanceRowDto>>;
