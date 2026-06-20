namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Serializes a set of attendance rows to a CSV file (FR-ADM-ATT-004). Synchronous and in-memory — fine for a
/// session cohort or a single student's sessions; a Hangfire async export is a later drop-in. The first column
/// is the row's key (student name for a session export, session title for a student export); the remaining
/// columns are the contract §B matrix metrics. Mirrors <see cref="ICodeExporter"/>.
/// </summary>
public interface IAttendanceExporter
{
    byte[] BuildCsv(string keyColumnHeader, IReadOnlyList<AttendanceExportRow> rows);
}

/// <summary>One CSV line of attendance data (decoupled from the feature DTOs so this lives in Common).</summary>
public sealed record AttendanceExportRow(
    string? KeyLabel,
    int VideosWatched,
    int VideosTotal,
    int? AssignmentPercent,
    int? BestQuizPercent,
    int QuizAttemptCount);
