using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Attendance.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Attendance.Queries.ExportStudentAttendance;

internal sealed class ExportStudentAttendanceHandler(
    IAppDbContext db, IAttendanceExporter exporter, IAuditWriter auditWriter, TimeProvider clock)
    : IRequestHandler<ExportStudentAttendanceQuery, AttendanceCsvFile>
{
    public async ValueTask<AttendanceCsvFile> Handle(
        ExportStudentAttendanceQuery query, CancellationToken cancellationToken)
    {
        var student = await db.Students
            .Where(s => s.Id == query.StudentId)
            .Select(s => new { s.FullName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Student", query.StudentId);

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == query.StudentId && e.Status != EnrollmentStatus.Refunded)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToListAsync(cancellationToken);

        var rows = await AttendanceProjector.ToStudentRowsAsync(db, query.StudentId, enrollments, cancellationToken);

        var csv = exporter.BuildCsv("Session", [.. rows.Select(r => new AttendanceExportRow(
            r.SessionTitle, r.VideosWatched, r.VideosTotal,
            r.AssignmentPercent, r.BestQuizPercent, r.QuizAttemptCount))]);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "AttendanceExported",
                EntityType: "Student",
                EntityId: query.StudentId,
                Summary: $"Exported attendance for {enrollments.Count} session(s) of student \"{student.FullName}\"."),
            cancellationToken);

        return new AttendanceCsvFile(csv, $"attendance-student-{clock.GetUtcNow():yyyyMMdd-HHmmss}.csv");
    }
}
