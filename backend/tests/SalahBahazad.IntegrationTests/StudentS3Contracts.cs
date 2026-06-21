namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Loosely-typed mirrors of the student-portal S3 My-Sessions API (GET /api/me/sessions[/{id}][/materials/.../url],
/// contract §A/§B/§C), kept separate from the production DTOs. Enums are string unions matching the API's
/// <c>JsonStringEnumConverter</c>: <c>State</c> ("NotStarted"|"InProgress"|"Completed"), <c>ProcessingStatus</c>
/// ("Pending"|"Processing"|"Ready"|"Failed"), <c>LockState</c>
/// ("Playable"|"QuizLocked"|"Expired"|"Exhausted"|"NotReady"), <c>GateState</c> ("Open"|"QuizRequired"|"Expired").
/// </summary>
public sealed record MySessionResponse(
    Guid Id,
    Guid EnrollmentId,
    string Title,
    string? GradeName,
    string? SubjectName,
    string? SpecializationName,
    string? ThumbnailUrl,
    int VideoCount,
    int VideosWatched,
    int ProgressPercent,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    string State);

public sealed record MySessionVideoResponse(
    Guid Id,
    string Title,
    int Order,
    int LengthSeconds,
    string ProcessingStatus,
    int AccessAllowed,
    int AccessRemaining,
    string LockState);

public sealed record MySessionMaterialResponse(Guid Id, string FileName, string Kind, long SizeBytes);

public sealed record MyAssignmentStatusResponse(
    Guid UserAssignmentId,
    string Status,
    int? ScoreMarks,
    int MaxMarks,
    int? CorrectCount,
    int QuestionCount,
    DateTimeOffset? CompletedAtUtc);

public sealed record MyQuizStatusResponse(
    Guid UserQuizId,
    bool Passed,
    int? BestPercent,
    int MinPassPercent,
    int AttemptsUsed,
    int AttemptCount,
    int TimeLimitMinutes,
    int QuestionCount);

public sealed record MySessionDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    Guid GradeId,
    string? GradeName,
    Guid SubjectId,
    string? SubjectName,
    Guid SpecializationId,
    string? SpecializationName,
    string? ThumbnailUrl,
    Guid EnrollmentId,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    int VideoCount,
    int VideosWatched,
    int ProgressPercent,
    string GateState,
    bool HasGatingQuiz,
    bool QuizPassed,
    int MinPassPercent,
    List<MySessionVideoResponse> Videos,
    List<MySessionMaterialResponse> Materials,
    MyAssignmentStatusResponse? Assignment,
    MyQuizStatusResponse? Quiz);

// SignedUrlResponse is shared with the admin material-URL contract — declared once in SessionContracts.cs.
