namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the Phase 5B-2 quiz API (kept separate from prod DTOs). Enums are strings
/// (the API's <c>JsonStringEnumConverter</c>).</summary>

// ── §A engine (student) ──────────────────────────────────────────────────────
public sealed record QuizSettings(int TimeLimitMinutes, int QuestionCount, int AttemptCount, int MinPassPercent);

public sealed record StudentQuizAttemptSummary(
    int Number, int? ScorePercent, string Status, string Flag, DateTimeOffset StartedAtUtc, DateTimeOffset? SubmittedAtUtc);

public sealed record StudentQuiz(
    Guid Id,
    Guid GatedSessionId,
    QuizSettings Settings,
    int AttemptsUsed,
    int AttemptsRemaining,
    int? BestPercent,
    bool Passed,
    Guid? ActiveAttemptId,
    List<StudentQuizAttemptSummary> Attempts);

public sealed record QuizOption(Guid Id, int Order, string Text);

public sealed record QuizQuestion(Guid Id, int Order, string? BodyLatex, string? ImageUrl, List<QuizOption> Options);

public sealed record QuizAttemptResponse(
    Guid AttemptId, int Number, DateTimeOffset DeadlineUtc, DateTimeOffset ServerNowUtc, List<QuizQuestion> Questions);

public sealed record QuizAttemptResult(int ScorePercent, string Status, int BestPercent, bool Passed, int AttemptsRemaining);

public sealed record QuizFocusBody(string Type, DateTimeOffset OccurredAtUtc, int? DurationMs);

// ── §B review (admin) ────────────────────────────────────────────────────────
public sealed record QuizReviewAttempt(
    int Number,
    int? ScorePercent,
    int TimeSpentSeconds,
    string Flag,
    string Status,
    DateTimeOffset StartedAtUtc,
    bool IsBest);

public sealed record QuizReview(
    int? BestPercent,
    bool Passed,
    int MinPassPercent,
    int AttemptsUsed,
    int AttemptsAllowed,
    List<QuizReviewAttempt> Attempts);
