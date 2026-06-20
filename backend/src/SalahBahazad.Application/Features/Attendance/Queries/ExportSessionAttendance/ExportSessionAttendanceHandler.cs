using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Attendance.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Attendance.Queries.ExportSessionAttendance;

internal sealed class ExportSessionAttendanceHandler(
    IAppDbContext db, IAttendanceExporter exporter, IAuditWriter auditWriter, TimeProvider clock)
    : IRequestHandler<ExportSessionAttendanceQuery, AttendanceCsvFile>
{
    public async ValueTask<AttendanceCsvFile> Handle(
        ExportSessionAttendanceQuery query, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Where(s => s.Id == query.SessionId)
            .Select(s => new { s.Title, VideosTotal = s.Videos.Count })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Session", query.SessionId);

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.SessionId == query.SessionId && e.Status != EnrollmentStatus.Refunded)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToListAsync(cancellationToken);

        var rows = await AttendanceProjector.ToSessionRowsAsync(
            db, query.SessionId, session.VideosTotal, enrollments, cancellationToken);

        var csv = exporter.BuildCsv("Student", [.. rows.Select(r => new AttendanceExportRow(
            r.StudentName, r.VideosWatched, r.VideosTotal,
            r.AssignmentPercent, r.BestQuizPercent, r.QuizAttemptCount))]);

        // A GET never reaches SaveChanges, so the interceptor cannot capture it — audit explicitly (FR-ADM-ATT-004).
        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "AttendanceExported",
                EntityType: "Session",
                EntityId: query.SessionId,
                Summary: $"Exported attendance for {enrollments.Count} enrollment(s) in session \"{session.Title}\"."),
            cancellationToken);

        return new AttendanceCsvFile(csv, $"attendance-session-{clock.GetUtcNow():yyyyMMdd-HHmmss}.csv");
    }
}
