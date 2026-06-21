namespace SalahBahazad.Application.Features.Attendance.DTOs;

/// <summary>
/// One enrolled student's row in a session's attendance matrix (contract §B #4, FR-ADM-ATT-001).
/// <c>videosWatched</c> = distinct videos with a spent view (derived from the per-video access counters the 5C
/// playback gate decrements); <c>bestQuizPercent</c>/<c>quizAttemptCount</c> from 5B-2 (null/0 when no gating
/// quiz). <c>assignmentPercent</c> is the auto-graded score, null until completion.
/// </summary>
public sealed record SessionAttendanceRowDto(
    Guid EnrollmentId,
    Guid StudentId,
    string? StudentName,
    int VideosWatched,
    int VideosTotal,
    int? AssignmentPercent,
    int? BestQuizPercent,
    int QuizAttemptCount);

/// <summary>One session's row in a student's per-session attendance breakdown (contract §B #5, FR-ADM-ATT-002).</summary>
public sealed record StudentAttendanceRowDto(
    Guid EnrollmentId,
    Guid SessionId,
    string? SessionTitle,
    int VideosWatched,
    int VideosTotal,
    int? AssignmentPercent,
    int? BestQuizPercent,
    int QuizAttemptCount);

/// <summary>A generated attendance CSV ready to stream as a file download (contract §B #6).</summary>
public sealed record AttendanceCsvFile(byte[] Content, string FileName);
