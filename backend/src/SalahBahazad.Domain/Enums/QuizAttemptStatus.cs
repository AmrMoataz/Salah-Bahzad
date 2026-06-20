namespace SalahBahazad.Domain.Enums;

/// <summary>
/// State of a single proctored <see cref="Entities.QuizAttempt"/> (FR-PLAT-QZ-004/005/009). An attempt is
/// started <see cref="InProgress"/> and reaches exactly one terminal state — never reopening
/// (FR-PLAT-QZ-009): <see cref="Submitted"/> (the student submitted), <see cref="TimedOut"/> (the server-side
/// timer auto-submitted at the deadline) or <see cref="Forfeited"/> (the single-sitting connection was lost).
/// </summary>
public enum QuizAttemptStatus
{
    /// <summary>Being taken; answers are recorded until submit/timeout/forfeit.</summary>
    InProgress = 0,

    /// <summary>The student submitted; graded and immutable.</summary>
    Submitted = 1,

    /// <summary>The single-sitting connection was lost (page close / disconnect) — scored 0 (FR-PLAT-QZ-004).</summary>
    Forfeited = 2,

    /// <summary>The deadline passed; the authoritative timer auto-submitted whatever was answered (FR-PLAT-QZ-005).</summary>
    TimedOut = 3,
}
