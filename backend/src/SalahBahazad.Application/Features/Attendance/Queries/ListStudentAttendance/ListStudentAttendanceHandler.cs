using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Attendance.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Attendance.Queries.ListStudentAttendance;

internal sealed class ListStudentAttendanceHandler(IAppDbContext db)
    : IRequestHandler<ListStudentAttendanceQuery, PagedResult<StudentAttendanceRowDto>>
{
    public async ValueTask<PagedResult<StudentAttendanceRowDto>> Handle(
        ListStudentAttendanceQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping is the global filter; 404 if the student is not the caller's.
        var exists = await db.Students.AnyAsync(s => s.Id == query.StudentId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Student", query.StudentId);

        var cohort = db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == query.StudentId && e.Status != EnrollmentStatus.Refunded);

        var total = await cohort.CountAsync(cancellationToken);
        var enrollments = await cohort
            .OrderByDescending(e => e.EnrolledAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var rows = await AttendanceProjector.ToStudentRowsAsync(db, query.StudentId, enrollments, cancellationToken);
        return new PagedResult<StudentAttendanceRowDto>(rows, total, query.Page, query.PageSize);
    }
}
