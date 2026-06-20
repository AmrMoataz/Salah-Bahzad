using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A student's gating quiz for an enrolment (FR-PLAT-QZ-001..010): generated from the <b>prerequisite</b>
/// session A's quiz-eligible bank and a <b>snapshot</b> of A's <see cref="QuizSetting"/> (time/count/attempts/
/// min-pass — immune to later edits, FR-PLAT-QZ-002, FR-PLAT-SES-006/007). Passing it (best-of ≥ min-pass)
/// unlocks the <b>gated</b> session B's videos (FR-PLAT-QZ-008 — the <c>≥</c> fix). One quiz per enrolment
/// (generation is idempotent). Tenant-scoped; owns its <see cref="QuizAttempt"/> sittings.
/// <see cref="IAuditViaEventOnly"/>: only its lifecycle events (generated/started/submitted/forfeited/timed-out)
/// leave an audit row — the per-answer saves do not (their behaviour goes to <see cref="AssessmentEvent"/>).
/// </summary>
public sealed class UserQuiz : TenantEntityBase, IAuditViaEventOnly
{
    private readonly List<QuizAttempt> _attempts = [];

    private UserQuiz() { }

    public Guid EnrollmentId { get; private set; }
    public Guid StudentId { get; private set; }

    /// <summary>The session whose videos this quiz gates (B — the enrolment's session, FR-PLAT-QZ-008).</summary>
    public Guid GatedSessionId { get; private set; }

    /// <summary>The prerequisite session the quiz is sourced from (A — bank + settings, FR-PLAT-QZ-002).</summary>
    public Guid SourceSessionId { get; private set; }

    // ── Snapshot of A's QuizSetting at generation time (immune to later edits, FR-PLAT-SES-007) ──────
    public int TimeLimitMinutes { get; private set; }
    public int QuestionCount { get; private set; }
    public int AttemptCount { get; private set; }
    public int MinPassPercent { get; private set; }

    /// <summary>Attempts started so far (an active sitting counts as used — it cannot be reclaimed).</summary>
    public int AttemptsUsed { get; private set; }

    /// <summary>Best percent across all graded attempts; null until the first attempt terminates.</summary>
    public int? BestPercent { get; private set; }

    /// <summary>True once <see cref="BestPercent"/> ≥ <see cref="MinPassPercent"/> — the videos-unlocked state
    /// the 5C gate reads (FR-PLAT-QZ-008, the <c>≥</c> fix #7).</summary>
    public bool Passed { get; private set; }

    public IReadOnlyCollection<QuizAttempt> Attempts => _attempts.AsReadOnly();

    public int AttemptsRemaining => Math.Max(0, AttemptCount - AttemptsUsed);

    /// <summary>The single in-progress attempt, or null when none is active.</summary>
    public QuizAttempt? ActiveAttempt => _attempts.FirstOrDefault(a => a.IsInProgress);

    /// <summary>
    /// Generates a student's gating quiz from the prerequisite's settings (FR-PLAT-QZ-001/002). The four
    /// <see cref="QuizSetting"/> knobs are <b>copied</b> so later edits to A's settings never change an issued
    /// quiz. Raises <see cref="QuizGeneratedEvent"/> (System-attributed). The questions themselves are drawn
    /// per-attempt at <see cref="StartAttempt"/>, not here, so each sitting is independently randomised.
    /// </summary>
    public static UserQuiz GenerateFor(
        Guid tenantId,
        Enrollment enrollment,
        Session gatedSession,
        Session sourceSession,
        QuizSetting setting,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentNullException.ThrowIfNull(gatedSession);
        ArgumentNullException.ThrowIfNull(sourceSession);
        ArgumentNullException.ThrowIfNull(setting);

        var quiz = new UserQuiz
        {
            EnrollmentId = enrollment.Id,
            StudentId = enrollment.StudentId,
            GatedSessionId = gatedSession.Id,
            SourceSessionId = sourceSession.Id,
            TimeLimitMinutes = setting.TimeLimitMinutes,
            QuestionCount = setting.QuestionCount,
            AttemptCount = setting.AttemptCount,
            MinPassPercent = setting.MinPassPercent,
            AttemptsUsed = 0,
            BestPercent = null,
            Passed = false,
        };
        quiz.SetTenant(tenantId);
        _ = now; // generation time is stamped by the audit interceptor; kept for signature symmetry with 5B-1.
        quiz.AddDomainEvent(new QuizGeneratedEvent(
            quiz.Id, quiz.StudentId, quiz.GatedSessionId, quiz.SourceSessionId));
        return quiz;
    }

    /// <summary>
    /// Starts a new attempt from the questions drawn for this sitting (FR-PLAT-QZ-003). Guards: no attempt may
    /// already be active and attempts must remain (either throws → 409). Counts the attempt as used immediately
    /// (so a lost connection forfeits it without giving it back, FR-PLAT-QZ-004) and raises
    /// <see cref="QuizAttemptStartedEvent"/> (student-attributed). The randomised subset + variation pick is the
    /// caller's (backend-owned); the domain snapshots whatever forms it is given.
    /// </summary>
    public QuizAttempt StartAttempt(IReadOnlyList<QuizQuestionForm> draws, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draws);
        if (ActiveAttempt is not null)
            throw new InvalidOperationException("An attempt is already in progress for this quiz.");
        if (AttemptsRemaining == 0)
            throw new InvalidOperationException("No quiz attempts remain.");

        var attempt = QuizAttempt.Start(AttemptsUsed + 1, draws, TimeLimitMinutes, now);
        _attempts.Add(attempt);
        AttemptsUsed++;
        AddDomainEvent(new QuizAttemptStartedEvent(
            Id, attempt.Id, attempt.Number, StudentId, TenantId, attempt.DeadlineUtc));
        return attempt;
    }

    /// <summary>Records the student's answer to one drawn question (guards in-progress + within deadline).</summary>
    public void Answer(Guid attemptId, Guid attemptQuestionId, Guid selectedOptionId, DateTimeOffset now)
        => RequireAttempt(attemptId).SelectAnswer(attemptQuestionId, selectedOptionId, now);

    /// <summary>
    /// Submits an in-progress attempt (FR-PLAT-QZ-007): grades it, recomputes best-of + pass, and raises the
    /// student-attributed <see cref="QuizAttemptSubmittedEvent"/> plus <see cref="QuizGradedEvent"/>. Submitting
    /// a terminal attempt throws (→ 409).
    /// </summary>
    public QuizAttempt SubmitAttempt(Guid attemptId, DateTimeOffset now)
    {
        var attempt = RequireAttempt(attemptId);
        attempt.Submit(now);
        RecomputeBest();
        AddDomainEvent(new QuizAttemptSubmittedEvent(Id, attempt.Id, attempt.Number, attempt.ScorePercent!.Value));
        AddDomainEvent(new QuizGradedEvent(StudentId, GatedSessionId, BestPercent!.Value, Passed));
        return attempt;
    }

    /// <summary>
    /// Auto-submits an attempt at its deadline (FR-PLAT-QZ-005, System-attributed). Idempotent: if the attempt
    /// is already terminal (a submit raced the timer) it is a no-op returning null, so the timer never overturns
    /// a real submission. Otherwise grades what was answered, status <c>TimedOut</c>, recomputes best-of + pass.
    /// </summary>
    public QuizAttempt? TimeOutAttempt(Guid attemptId, DateTimeOffset now)
    {
        var attempt = _attempts.FirstOrDefault(a => a.Id == attemptId);
        if (attempt is null || !attempt.IsInProgress)
            return null;

        _ = now; // the deadline is authoritative; the timeout instant is the snapshotted DeadlineUtc.
        attempt.TimeOut();
        RecomputeBest();
        AddDomainEvent(new QuizAttemptTimedOutEvent(Id, attempt.Id, attempt.Number, attempt.ScorePercent!.Value));
        AddDomainEvent(new QuizGradedEvent(StudentId, GatedSessionId, BestPercent!.Value, Passed));
        return attempt;
    }

    /// <summary>
    /// Forfeits the active attempt on a lost single-sitting connection (FR-PLAT-QZ-004, System-attributed):
    /// scored 0, status <c>Forfeited</c>, consuming it. Idempotent: no active attempt ⇒ no-op returning null
    /// (a disconnect after a clean submit changes nothing). A 0 never beats the best-of, so no grade event.
    /// </summary>
    public QuizAttempt? ForfeitActiveAttempt(DateTimeOffset now)
    {
        var attempt = ActiveAttempt;
        if (attempt is null)
            return null;

        attempt.Forfeit(now);
        RecomputeBest();
        AddDomainEvent(new QuizAttemptForfeitedEvent(Id, attempt.Id, attempt.Number));
        return attempt;
    }

    private QuizAttempt RequireAttempt(Guid attemptId)
        => _attempts.FirstOrDefault(a => a.Id == attemptId)
           ?? throw new InvalidOperationException($"Attempt '{attemptId}' is not part of this quiz.");

    private void RecomputeBest()
    {
        var graded = _attempts.Where(a => a.ScorePercent.HasValue).Select(a => a.ScorePercent!.Value).ToList();
        BestPercent = graded.Count == 0 ? null : graded.Max();
        Passed = BestPercent is int best && best >= MinPassPercent; // FR-PLAT-QZ-008: ≥, not > (#7).
    }
}

/// <summary>
/// The question form drawn for one slot of a <see cref="QuizAttempt"/> — a bank question or one of its
/// variations (FR-PLAT-QB-003), already chosen by the backend's randomisation strategy. Carries the provenance
/// id, the renderable body/image, the mark, and the option set to copy into the immutable snapshot. The options
/// are copied into the attempt, never shared.
/// </summary>
public sealed record QuizQuestionForm(
    Guid QuestionId, string? BodyLatex, string? ImageObjectKey, int Mark, IReadOnlyList<QuestionOption> Options);
