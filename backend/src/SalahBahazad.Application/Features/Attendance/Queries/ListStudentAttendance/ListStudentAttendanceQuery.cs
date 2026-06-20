using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Attendance.DTOs;

namespace SalahBahazad.Application.Features.Attendance.Queries.ListStudentAttendance;

/// <summary>
/// A student's per-session attendance breakdown (contract §B #5, FR-ADM-ATT-002): one row per
/// <c>Active</c>/<c>Expired</c> enrollment with its session title, video total and <c>Attendance</c> metrics, paged.
/// </summary>
public sealed record ListStudentAttendanceQuery(Guid StudentId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<StudentAttendanceRowDto>>;
