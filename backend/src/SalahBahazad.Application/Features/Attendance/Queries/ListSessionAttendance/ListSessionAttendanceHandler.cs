using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Attendance.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Attendance.Queries.ListSessionAttendance;

internal sealed class ListSessionAttendanceHandler(IAppDbContext db)
    : IRequestHandler<ListSessionAttendanceQuery, PagedResult<SessionAttendanceRowDto>>
{
    public async ValueTask<PagedResult<SessionAttendanceRowDto>> Handle(
        ListSessionAttendanceQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping is the global filter; 404 if the session is not the caller's.
        var session = await db.Sessions
            .Where(s => s.Id == query.SessionId)
            .Select(s => new { VideosTotal = s.Videos.Count })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Session", query.SessionId);

        // Cohort = the session's Active/Expired enrollments (Refunded excluded; soft-deleted hidden by the filter).
        var cohort = db.Enrollments
            .AsNoTracking()
            .Where(e => e.SessionId == query.SessionId && e.Status != EnrollmentStatus.Refunded);

        var total = await cohort.CountAsync(cancellationToken);
        var enrollments = await cohort
            .OrderByDescending(e => e.EnrolledAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var rows = await AttendanceProjector.ToSessionRowsAsync(
            db, query.SessionId, session.VideosTotal, enrollments, cancellationToken);

        return new PagedResult<SessionAttendanceRowDto>(rows, total, query.Page, query.PageSize);
    }
}
