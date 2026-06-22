using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;
using QuizAttemptEntity = SalahBahazad.Domain.Entities.QuizAttempt;
using UserQuizEntity = SalahBahazad.Domain.Entities.UserQuiz;

namespace SalahBahazad.Application.Features.Quizzes.DTOs;

/// <summary>The snapshotted quiz settings as shown to the student (contract §A).</summary>
public sealed record QuizSettingsDto(int TimeLimitMinutes, int QuestionCount, int AttemptCount, int MinPassPercent);

/// <summary>One past/active attempt in the student's quiz summary (contract §A #1). <c>Id</c> is purely additive
/// (S5): it makes each terminal attempt addressable by the per-attempt review (§B).</summary>
public sealed record StudentQuizAttemptSummaryDto(
    Guid Id,
    int Number,
    int? ScorePercent,
    QuizAttemptStatus Status,
    string Flag,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? SubmittedAtUtc);

/// <summary>The caller's gating quiz for a session (contract §A #1). Summary only — no questions/answers.</summary>
public sealed record StudentQuizDto(
    Guid Id,
    Guid GatedSessionId,
    QuizSettingsDto Settings,
    int AttemptsUsed,
    int AttemptsRemaining,
    int? BestPercent,
    bool Passed,
    Guid? ActiveAttemptId,
    IReadOnlyList<StudentQuizAttemptSummaryDto> Attempts);

/// <summary>One MCQ option as shown to the student — no correctness leaked (contract §A #2).</summary>
public sealed record QuizOptionDto(Guid Id, int Order, string Text);

/// <summary>One drawn question as shown to the student. <c>imageUrl</c> is a short-lived signed URL; no
/// <c>isCorrect</c> and no hint are exposed (FR-PLAT-QB-005 hints are assignment-only).</summary>
public sealed record QuizQuestionDto(
    Guid Id, int Order, string? BodyLatex, string? ImageUrl, IReadOnlyList<QuizOptionDto> Options);

/// <summary>A started attempt with its randomised question set and authoritative deadline (contract §A #2).</summary>
public sealed record QuizAttemptDto(
    Guid AttemptId,
    int Number,
    DateTimeOffset DeadlineUtc,
    DateTimeOffset ServerNowUtc,
    IReadOnlyList<QuizQuestionDto> Questions);

/// <summary>The graded result of a submitted attempt (contract §A #4).</summary>
public sealed record QuizAttemptResultDto(
    int ScorePercent, QuizAttemptStatus Status, int BestPercent, bool Passed, int AttemptsRemaining);

/// <summary>One MCQ option in the per-attempt <b>answer-key review</b> — <c>isCorrect</c> is exposed (contract
/// §B.1). This is the only student surface that reveals quiz correctness, and only for the caller's own
/// <b>terminal</b> attempt; the live <see cref="QuizOptionDto"/> stays correctness-free (the 5B-2 invariant).</summary>
public sealed record StudentQuizReviewOptionDto(Guid Id, int Order, string Text, bool IsCorrect);

/// <summary>One drawn question in the answer-key review: the immutable snapshot plus each option's correctness,
/// the student's pick this attempt, and whether that pick was the correct one (contract §B.1). No <c>hintUrl</c>
/// (quiz questions carry none, FR-PLAT-QB-005).</summary>
public sealed record StudentQuizReviewQuestionDto(
    Guid Id,
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    IReadOnlyList<StudentQuizReviewOptionDto> Options,
    Guid? SelectedOptionId,
    bool IsCorrect);

/// <summary>
/// The caller's own <b>terminal</b> quiz attempt with the answer key and this attempt's score (contract §B.1,
/// FR-STU-QZ-009) — a <b>distinct</b> DTO so the live <see cref="QuizAttemptDto"/>/<see cref="QuizQuestionDto"/>/
/// <see cref="QuizOptionDto"/> are never widened with correctness. The quiz-level pass/best-of lives on the intro
/// <see cref="StudentQuizDto"/>; here <see cref="ScorePercent"/>/<see cref="MinPassPercent"/> drive the per-attempt
/// pass/fail chip (client-side <c>scorePercent &gt;= minPassPercent</c>).
/// </summary>
public sealed record StudentQuizAttemptReviewDto(
    Guid AttemptId,
    Guid QuizId,
    Guid GatedSessionId,
    string? SessionTitle,
    int Number,
    QuizAttemptStatus Status,
    int ScorePercent,
    int MinPassPercent,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset SubmittedAtUtc,
    int TimeSpentSeconds,
    IReadOnlyList<StudentQuizReviewQuestionDto> Questions);

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md). The student shape never carries
/// option correctness; image keys are signed on read.</summary>
public static class QuizMappings
{
    public static StudentQuizDto ToStudentDto(this UserQuizEntity quiz)
    {
        var attempts = quiz.Attempts
            .OrderBy(a => a.Number)
            .Select(a => new StudentQuizAttemptSummaryDto(
                a.Id, a.Number, a.ScorePercent, a.Status, QuizAttemptFlag.For(a.Status), a.StartedAtUtc, a.SubmittedAtUtc))
            .ToList();

        return new StudentQuizDto(
            quiz.Id,
            quiz.GatedSessionId,
            new QuizSettingsDto(quiz.TimeLimitMinutes, quiz.QuestionCount, quiz.AttemptCount, quiz.MinPassPercent),
            quiz.AttemptsUsed,
            quiz.AttemptsRemaining,
            quiz.BestPercent,
            quiz.Passed,
            quiz.ActiveAttempt?.Id,
            attempts);
    }

    public static async Task<QuizAttemptDto> ToAttemptDtoAsync(
        this QuizAttemptEntity attempt,
        DateTimeOffset serverNowUtc,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var questions = new List<QuizQuestionDto>();
        foreach (var q in attempt.Questions.OrderBy(q => q.Order))
        {
            var imageUrl = await SignAsync(q.ImageObjectKey, fileStorage, cancellationToken);
            questions.Add(new QuizQuestionDto(
                q.Id,
                q.Order,
                q.BodyLatex,
                imageUrl,
                [.. q.Options.OrderBy(o => o.Order).Select(o => new QuizOptionDto(o.Id, o.Order, o.Text))]));
        }

        return new QuizAttemptDto(attempt.Id, attempt.Number, attempt.DeadlineUtc, serverNowUtc, questions);
    }

    public static QuizAttemptResultDto ToResultDto(this UserQuizEntity quiz, QuizAttemptEntity attempt)
        => new(attempt.ScorePercent ?? 0, attempt.Status, quiz.BestPercent ?? 0, quiz.Passed, quiz.AttemptsRemaining);

    /// <summary>Maps a <b>terminal</b> attempt to the per-attempt answer-key review (contract §B.1) — the only
    /// student projection that exposes option correctness. The caller has already resolved the attempt through its
    /// owning <paramref name="quiz"/> (the IDOR/tenant scope), gated <c>Status != InProgress</c>, and resolved
    /// <paramref name="sessionTitle"/> (via <c>IgnoreQueryFilters</c>). Quiz-level fields (id, gated session,
    /// min-pass) come from the root; per-attempt fields and the immutable snapshot from <paramref name="attempt"/>.
    /// Questions ordered by <c>Order</c> (1-based), options by <c>Order</c> (0-based). The terminal gate guarantees
    /// <c>SubmittedAtUtc</c>/<c>ScorePercent</c> are set; <c>?? default</c>/<c>?? 0</c> are defensive only.</summary>
    public static async Task<StudentQuizAttemptReviewDto> ToReviewDtoAsync(
        this UserQuizEntity quiz,
        QuizAttemptEntity attempt,
        string? sessionTitle,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var questions = new List<StudentQuizReviewQuestionDto>();
        foreach (var q in attempt.Questions.OrderBy(q => q.Order))
        {
            var imageUrl = await SignAsync(q.ImageObjectKey, fileStorage, cancellationToken);
            questions.Add(new StudentQuizReviewQuestionDto(
                q.Id,
                q.Order,
                q.BodyLatex,
                imageUrl,
                q.Mark,
                [.. q.Options.OrderBy(o => o.Order).Select(o => new StudentQuizReviewOptionDto(o.Id, o.Order, o.Text, o.IsCorrect))],
                q.SelectedOptionId,
                q.IsCorrect));
        }

        return new StudentQuizAttemptReviewDto(
            attempt.Id,
            quiz.Id,
            quiz.GatedSessionId,
            sessionTitle,
            attempt.Number,
            attempt.Status,
            attempt.ScorePercent ?? 0,
            quiz.MinPassPercent,
            attempt.StartedAtUtc,
            attempt.SubmittedAtUtc ?? default,
            attempt.TimeSpentSeconds,
            questions);
    }

    private static async Task<string?> SignAsync(
        string? objectKey, IFileStorage fileStorage, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(objectKey)
            ? null
            : (await fileStorage.GetSignedReadUrlAsync(objectKey, cancellationToken: cancellationToken)).Url;
}
