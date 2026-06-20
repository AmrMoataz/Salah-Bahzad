using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Attendance.DTOs;
using EnrollmentEntity = SalahBahazad.Domain.Entities.Enrollment;

namespace SalahBahazad.Application.Features.Attendance;

/// <summary>
/// Resolves the display joins for an attendance cohort (student/session names, video totals, the
/// per-(student, session) <c>Attendance</c> metrics) and projects them to the matrix DTOs — shared by the paged
/// reads and the CSV exports so they always agree (FR-ADM-ATT-001/002). Names are looked up with
/// <c>IgnoreQueryFilters</c> so an archived session/student still shows its name; the ids all come from the
/// caller's tenant-scoped enrollments. Mirrors <c>CodeListProjector</c>.
/// </summary>
internal static class AttendanceProjector
{
    public static async Task<List<SessionAttendanceRowDto>> ToSessionRowsAsync(
        IAppDbContext db,
        Guid sessionId,
        int videosTotal,
        IReadOnlyList<EnrollmentEntity> enrollments,
        CancellationToken cancellationToken)
    {
        if (enrollments.Count == 0)
            return [];

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

        var studentNames = await db.Students
            .IgnoreQueryFilters()
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var attendanceByStudent = (await db.Attendances
                .Where(a => a.SessionId == sessionId && studentIds.Contains(a.StudentId))
                .Select(a => new { a.StudentId, a.AssignmentScore, a.VideosWatched })
                .ToListAsync(cancellationToken))
            .ToDictionary(a => a.StudentId);

        return [.. enrollments.Select(e =>
        {
            attendanceByStudent.TryGetValue(e.StudentId, out var att);
            return new SessionAttendanceRowDto(
                EnrollmentId: e.Id,
                StudentId: e.StudentId,
                StudentName: studentNames.GetValueOrDefault(e.StudentId),
                VideosWatched: att?.VideosWatched ?? 0,
                VideosTotal: videosTotal,
                AssignmentPercent: att?.AssignmentScore,
                BestQuizPercent: null,   // 5B-2
                QuizAttemptCount: 0);    // 5B-2
        })];
    }

    public static async Task<List<StudentAttendanceRowDto>> ToStudentRowsAsync(
        IAppDbContext db,
        Guid studentId,
        IReadOnlyList<EnrollmentEntity> enrollments,
        CancellationToken cancellationToken)
    {
        if (enrollments.Count == 0)
            return [];

        var sessionIds = enrollments.Select(e => e.SessionId).Distinct().ToList();

        var sessionTitles = await db.Sessions
            .IgnoreQueryFilters()
            .Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Title, cancellationToken);

        var videoTotals = (await db.SessionVideos
                .IgnoreQueryFilters()
                .Where(v => sessionIds.Contains(v.SessionId))
                .GroupBy(v => v.SessionId)
                .Select(g => new { SessionId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.SessionId, x => x.Count);

        var attendanceBySession = (await db.Attendances
                .Where(a => a.StudentId == studentId && sessionIds.Contains(a.SessionId))
                .Select(a => new { a.SessionId, a.AssignmentScore, a.VideosWatched })
                .ToListAsync(cancellationToken))
            .ToDictionary(a => a.SessionId);

        return [.. enrollments.Select(e =>
        {
            attendanceBySession.TryGetValue(e.SessionId, out var att);
            return new StudentAttendanceRowDto(
                EnrollmentId: e.Id,
                SessionId: e.SessionId,
                SessionTitle: sessionTitles.GetValueOrDefault(e.SessionId),
                VideosWatched: att?.VideosWatched ?? 0,
                VideosTotal: videoTotals.GetValueOrDefault(e.SessionId),
                AssignmentPercent: att?.AssignmentScore,
                BestQuizPercent: null,   // 5B-2
                QuizAttemptCount: 0);    // 5B-2
        })];
    }
}
