using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// One proctored attempt at a <see cref="UserQuiz"/> — an <b>immutable snapshot</b> of the questions drawn for
/// this sitting (FR-PLAT-QZ-003, snapshot fairness FR-PLAT-SES-007), plus the student's answers and the graded
/// result. Each attempt independently draws a randomised subset of the source bank, so later bank edits never
/// alter an issued attempt. Reaches exactly one terminal <see cref="QuizAttemptStatus"/> and never reopens
/// (FR-PLAT-QZ-009). Owned by the <see cref="UserQuiz"/> root; <see cref="IAuditViaEventOnly"/> so per-answer
/// saves and the snapshot rows never emit field-diff audit entries (the lifecycle events on the root do).
/// </summary>
public sealed class QuizAttempt : IAuditViaEventOnly
{
    private readonly List<QuizAttemptQuestion> _questions = [];

    private QuizAttempt() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>One-based sitting number within the quiz.</summary>
    public int Number { get; private set; }

    public QuizAttemptStatus Status { get; private set; } = QuizAttemptStatus.InProgress;

    /// <summary>Graded 0–100 percent; null until the attempt terminates.</summary>
    public int? ScorePercent { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    /// <summary>The authoritative auto-submit instant (start + the snapshotted time limit, FR-PLAT-QZ-005).</summary>
    public DateTimeOffset DeadlineUtc { get; private set; }

    /// <summary>When the attempt terminated (submit/forfeit instant; the deadline on timeout); null while active.</summary>
    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public IReadOnlyCollection<QuizAttemptQuestion> Questions => _questions.AsReadOnly();

    public bool IsInProgress => Status == QuizAttemptStatus.InProgress;

    /// <summary>Elapsed seconds the sitting took (submitted−started, or the full window on timeout); 0 while active.</summary>
    public int TimeSpentSeconds => SubmittedAtUtc is DateTimeOffset ended
        ? Math.Max(0, (int)Math.Round((ended - StartedAtUtc).TotalSeconds, MidpointRounding.AwayFromZero))
        : 0;

    /// <summary>Snapshots the drawn questions into a fresh in-progress attempt; the deadline is start + time limit.</summary>
    internal static QuizAttempt Start(
        int number, IReadOnlyList<QuizQuestionForm> draws, int timeLimitMinutes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draws);
        if (draws.Count == 0)
            throw new InvalidOperationException("Cannot start a quiz attempt with no questions.");

        var attempt = new QuizAttempt
        {
            Number = number,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = now,
            DeadlineUtc = now.AddMinutes(timeLimitMinutes),
        };

        for (var i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            attempt._questions.Add(QuizAttemptQuestion.Snapshot(
                questionId: draw.QuestionId,
                order: i + 1, // 1-based position within the attempt (contract §A)
                bodyLatex: draw.BodyLatex,
                imageObjectKey: draw.ImageObjectKey,
                mark: draw.Mark,
                options: draw.Options));
        }

        return attempt;
    }

    /// <summary>Records an answer; rejected once the attempt is terminal or past its deadline (→ 409).</summary>
    internal void SelectAnswer(Guid attemptQuestionId, Guid selectedOptionId, DateTimeOffset now)
    {
        if (Status != QuizAttemptStatus.InProgress)
            throw new InvalidOperationException("This quiz attempt is no longer in progress.");
        if (now > DeadlineUtc)
            throw new InvalidOperationException("This quiz attempt's time limit has elapsed.");

        var question = _questions.FirstOrDefault(q => q.Id == attemptQuestionId)
            ?? throw new InvalidOperationException(
                $"Question '{attemptQuestionId}' is not part of this attempt.");

        question.SelectAnswer(selectedOptionId, now);
    }

    /// <summary>Grades the answered questions and seals the attempt as <see cref="QuizAttemptStatus.Submitted"/>.</summary>
    internal void Submit(DateTimeOffset now)
    {
        EnsureInProgress();
        Status = QuizAttemptStatus.Submitted;
        SubmittedAtUtc = now;
        ScorePercent = ComputeScorePercent();
    }

    /// <summary>Auto-submits at the deadline (FR-PLAT-QZ-005): grades what was answered, status
    /// <see cref="QuizAttemptStatus.TimedOut"/>; the time spent is the full window.</summary>
    internal void TimeOut()
    {
        EnsureInProgress();
        Status = QuizAttemptStatus.TimedOut;
        SubmittedAtUtc = DeadlineUtc;
        ScorePercent = ComputeScorePercent();
    }

    /// <summary>Forfeits on a lost single-sitting connection (FR-PLAT-QZ-004): scored 0, status
    /// <see cref="QuizAttemptStatus.Forfeited"/>.</summary>
    internal void Forfeit(DateTimeOffset now)
    {
        EnsureInProgress();
        Status = QuizAttemptStatus.Forfeited;
        SubmittedAtUtc = now;
        ScorePercent = 0;
    }

    private void EnsureInProgress()
    {
        if (Status != QuizAttemptStatus.InProgress)
            throw new InvalidOperationException("This quiz attempt has already ended and cannot change.");
    }

    private int ComputeScorePercent()
    {
        var maxMarks = _questions.Sum(q => q.Mark);
        if (maxMarks == 0) return 0;
        var scored = _questions.Where(q => q.IsCorrect).Sum(q => q.Mark);
        return (int)Math.Round(scored * 100.0 / maxMarks, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// One drawn question of a <see cref="QuizAttempt"/> — an <b>immutable snapshot</b> of a bank question or one of
/// its variations (FR-PLAT-QZ-003, FR-PLAT-SES-007). Body, image, mark and the option set are frozen copies;
/// only the student's <see cref="SelectedOptionId"/> is mutable, and only while the attempt is in progress.
/// No hint is carried — hints are an assignment-only aid (FR-PLAT-QB-005). Owned by the attempt;
/// <see cref="IAuditViaEventOnly"/> so it never emits a row of its own.
/// </summary>
public sealed class QuizAttemptQuestion : IAuditViaEventOnly
{
    private readonly List<QuizAttemptOption> _options = [];

    private QuizAttemptQuestion() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>The originating bank question (provenance only — never re-read for content).</summary>
    public Guid QuestionId { get; private set; }

    /// <summary>One-based position within the attempt.</summary>
    public int Order { get; private set; }

    public string? BodyLatex { get; private set; }

    /// <summary>R2 object key for the question image; null when the body is LaTeX-only.</summary>
    public string? ImageObjectKey { get; private set; }

    public int Mark { get; private set; }

    /// <summary>The student's chosen option; null until answered.</summary>
    public Guid? SelectedOptionId { get; private set; }

    public DateTimeOffset? AnsweredAtUtc { get; private set; }

    public IReadOnlyCollection<QuizAttemptOption> Options => _options.AsReadOnly();

    public bool IsAnswered => SelectedOptionId is not null;

    /// <summary>True when the student's selected option is the correct one.</summary>
    public bool IsCorrect => SelectedOptionId is Guid selected
        && _options.Any(o => o.Id == selected && o.IsCorrect);

    internal static QuizAttemptQuestion Snapshot(
        Guid questionId,
        int order,
        string? bodyLatex,
        string? imageObjectKey,
        int mark,
        IReadOnlyList<QuestionOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0)
            throw new InvalidOperationException("A drawn quiz question must carry its options.");

        var question = new QuizAttemptQuestion
        {
            QuestionId = questionId,
            Order = order,
            BodyLatex = bodyLatex,
            ImageObjectKey = imageObjectKey,
            Mark = mark,
        };
        // Copy each option into a fresh snapshot value so later bank edits never bleed through.
        foreach (var option in options.OrderBy(o => o.Order))
            question._options.Add(QuizAttemptOption.Copy(option.Order, option.Text, option.IsCorrect));
        return question;
    }

    internal void SelectAnswer(Guid optionId, DateTimeOffset now)
    {
        if (_options.All(o => o.Id != optionId))
            throw new InvalidOperationException("The selected option does not belong to this question.");

        SelectedOptionId = optionId;
        AnsweredAtUtc = now;
    }
}

/// <summary>
/// One snapshotted MCQ option of a <see cref="QuizAttemptQuestion"/> — a frozen copy of a bank option's text,
/// order and correctness, with its own stable id the student selects by. Owned value, immutable once written;
/// <see cref="IAuditViaEventOnly"/>. Correctness is never exposed to the student shape — only the staff review.
/// </summary>
public sealed class QuizAttemptOption : IAuditViaEventOnly
{
    private QuizAttemptOption() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>Zero-based display position within the snapshotted option set.</summary>
    public int Order { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public bool IsCorrect { get; private set; }

    internal static QuizAttemptOption Copy(int order, string text, bool isCorrect)
        => new() { Order = order, Text = text, IsCorrect = isCorrect };
}
