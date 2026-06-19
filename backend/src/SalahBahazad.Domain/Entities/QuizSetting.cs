namespace SalahBahazad.Domain.Entities;

/// <summary>
/// The gating-quiz configuration owned 1:1 by a <see cref="Session"/> (FR-PLAT-SES-006, FR-ADM-QZ-001).
/// These knobs apply to the quiz that gates the <b>next</b> session (the one for which this session is the
/// prerequisite), drawing from this session's quiz-eligible questions (FR-PLAT-QZ-001/002). An owned value
/// type — it has no identity of its own and is replaced wholesale via <see cref="Session.UpdateQuizSettings"/>.
/// Tight product ranges (time 5–60, questions 5–30, attempts 1–5, pass 40–100) are enforced at the
/// validation layer; the domain keeps only the logical invariants as a backstop.
/// </summary>
public sealed class QuizSetting
{
    private QuizSetting() { }

    /// <summary>Total quiz time in minutes (admin-entered, FR-ADM-QZ-001).</summary>
    public int TimeLimitMinutes { get; private set; }

    /// <summary>Number of questions drawn per attempt (≤ the session's quiz-eligible count, FR-ADM-QZ-002).</summary>
    public int QuestionCount { get; private set; }

    /// <summary>Allowed attempts per enrollment (FR-PLAT-QZ-002).</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Minimum percentage to pass and unlock the gated session (FR-PLAT-QZ-008, <c>≥</c>).</summary>
    public int MinPassPercent { get; private set; }

    public static QuizSetting Create(int timeLimitMinutes, int questionCount, int attemptCount, int minPassPercent)
    {
        if (timeLimitMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeLimitMinutes), "Quiz time limit must be positive.");
        if (questionCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(questionCount), "Quiz question count must be positive.");
        if (attemptCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptCount), "Quiz attempt count must be positive.");
        if (minPassPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(minPassPercent), "Minimum pass percent must be 0–100.");

        return new QuizSetting
        {
            TimeLimitMinutes = timeLimitMinutes,
            QuestionCount = questionCount,
            AttemptCount = attemptCount,
            MinPassPercent = minPassPercent,
        };
    }
}
