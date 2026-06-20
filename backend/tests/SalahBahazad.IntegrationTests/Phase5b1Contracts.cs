namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the Phase 5B-1 assignment/attendance/review API (kept separate from prod DTOs).</summary>

// ── §A engine (student) ──────────────────────────────────────────────────────
public sealed record StudentAssignmentOption(Guid Id, int Order, string Text);

public sealed record StudentAssignmentQuestion(
    Guid Id,
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    string? HintUrl,
    List<StudentAssignmentOption> Options,
    Guid? SelectedOptionId);

public sealed record StudentAssignmentResponse(
    Guid Id,
    Guid SessionId,
    string Status,
    int TimeSpentSeconds,
    List<StudentAssignmentQuestion> Questions);

public sealed record AssignmentProgress(int AnsweredCount, int QuestionCount, string Status);

public sealed record AnswerBody(Guid SelectedOptionId);

public sealed record RecordEventBody(string Type, int? QuestionOrder, DateTimeOffset OccurredAtUtc, int? ElapsedMs);

// ── §B attendance (admin) ────────────────────────────────────────────────────
public sealed record SessionAttendanceRow(
    Guid EnrollmentId,
    Guid StudentId,
    string? StudentName,
    int VideosWatched,
    int VideosTotal,
    int? AssignmentPercent,
    int? BestQuizPercent,
    int QuizAttemptCount);

public sealed record PagedSessionAttendance(List<SessionAttendanceRow> Items, int Total, int Page, int PageSize);

public sealed record StudentAttendanceRow(
    Guid EnrollmentId,
    Guid SessionId,
    string? SessionTitle,
    int VideosWatched,
    int VideosTotal,
    int? AssignmentPercent,
    int? BestQuizPercent,
    int QuizAttemptCount);

public sealed record PagedStudentAttendance(List<StudentAttendanceRow> Items, int Total, int Page, int PageSize);

// ── §C review (admin) ────────────────────────────────────────────────────────
public sealed record ReviewOption(Guid Id, int Order, string Text, bool IsCorrect);

public sealed record ReviewQuestion(
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    string? HintUrl,
    List<ReviewOption> Options,
    Guid? SelectedOptionId,
    bool IsCorrect);

public sealed record AssignmentReview(
    string? StudentName,
    string? SessionTitle,
    int CorrectCount,
    int QuestionCount,
    int ScoreMarks,
    int MaxMarks,
    int Percent,
    int TimeSpentSeconds,
    string Status,
    List<ReviewQuestion> Questions);

public sealed record BehaviourEvent(string Type, string Label, int? QuestionOrder, DateTimeOffset OccurredAtUtc);
