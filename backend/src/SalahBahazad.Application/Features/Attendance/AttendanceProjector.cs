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
        var enrollmentIds = enrollments.Select(e => e.Id).Distinct().ToList();

        var studentNames = await db.Students
            .IgnoreQueryFilters()
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var attendanceByStudent = (await db.Attendances
                .Where(a => a.SessionId == sessionId && studentIds.Contains(a.StudentId))
                .Select(a => new { a.StudentId, a.AssignmentScore, a.VideosWatched })
                .ToListAsync(cancellationToken))
            .ToDictionary(a => a.StudentId);

        // Quiz best-of + attempt count, joined per enrollment (5B-2; null/0 when no quiz gates the session).
        var quizByEnrollment = await QuizByEnrollmentAsync(db, enrollmentIds, cancellationToken);

        return [.. enrollments.Select(e =>
        {
            attendanceByStudent.TryGetValue(e.StudentId, out var att);
            quizByEnrollment.TryGetValue(e.Id, out var quiz);
            return new SessionAttendanceRowDto(
                EnrollmentId: e.Id,
                StudentId: e.StudentId,
                StudentName: studentNames.GetValueOrDefault(e.StudentId),
                VideosWatched: att?.VideosWatched ?? 0,
                VideosTotal: videosTotal,
                AssignmentPercent: att?.AssignmentScore,
                BestQuizPercent: quiz?.BestPercent,
                QuizAttemptCount: quiz?.AttemptsUsed ?? 0);
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
        var enrollmentIds = enrollments.Select(e => e.Id).Distinct().ToList();

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

        // Quiz best-of + attempt count, joined per enrollment (5B-2; null/0 when no quiz gates the session).
        var quizByEnrollment = await QuizByEnrollmentAsync(db, enrollmentIds, cancellationToken);

        return [.. enrollments.Select(e =>
        {
            attendanceBySession.TryGetValue(e.SessionId, out var att);
            quizByEnrollment.TryGetValue(e.Id, out var quiz);
            return new StudentAttendanceRowDto(
                EnrollmentId: e.Id,
                SessionId: e.SessionId,
                SessionTitle: sessionTitles.GetValueOrDefault(e.SessionId),
                VideosWatched: att?.VideosWatched ?? 0,
                VideosTotal: videoTotals.GetValueOrDefault(e.SessionId),
                AssignmentPercent: att?.AssignmentScore,
                BestQuizPercent: quiz?.BestPercent,
                QuizAttemptCount: quiz?.AttemptsUsed ?? 0);
        })];
    }

    /// <summary>The best-of percent + attempts-used for each enrollment's quiz (FR-PLAT-ATT-002), keyed by
    /// enrollment id. Empty for enrollments with no gating quiz.</summary>
    private static async Task<Dictionary<Guid, QuizMetrics>> QuizByEnrollmentAsync(
        IAppDbContext db, IReadOnlyList<Guid> enrollmentIds, CancellationToken cancellationToken)
        => (await db.UserQuizzes
                .AsNoTracking()
                .Where(q => enrollmentIds.Contains(q.EnrollmentId))
                .Select(q => new { q.EnrollmentId, q.BestPercent, q.AttemptsUsed })
                .ToListAsync(cancellationToken))
            .ToDictionary(q => q.EnrollmentId, q => new QuizMetrics(q.BestPercent, q.AttemptsUsed));

    private sealed record QuizMetrics(int? BestPercent, int AttemptsUsed);
}
