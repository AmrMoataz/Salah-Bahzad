using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;
using QuizAttemptEntity = SalahBahazad.Domain.Entities.QuizAttempt;
using UserQuizEntity = SalahBahazad.Domain.Entities.UserQuiz;

namespace SalahBahazad.Application.Features.Quizzes.DTOs;

/// <summary>The snapshotted quiz settings as shown to the student (contract §A).</summary>
public sealed record QuizSettingsDto(int TimeLimitMinutes, int QuestionCount, int AttemptCount, int MinPassPercent);

/// <summary>One past/active attempt in the student's quiz summary (contract §A #1).</summary>
public sealed record StudentQuizAttemptSummaryDto(
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

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md). The student shape never carries
/// option correctness; image keys are signed on read.</summary>
public static class QuizMappings
{
    public static StudentQuizDto ToStudentDto(this UserQuizEntity quiz)
    {
        var attempts = quiz.Attempts
            .OrderBy(a => a.Number)
            .Select(a => new StudentQuizAttemptSummaryDto(
                a.Number, a.ScorePercent, a.Status, QuizAttemptFlag.For(a.Status), a.StartedAtUtc, a.SubmittedAtUtc))
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

    private static async Task<string?> SignAsync(
        string? objectKey, IFileStorage fileStorage, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(objectKey)
            ? null
            : (await fileStorage.GetSignedReadUrlAsync(objectKey, cancellationToken: cancellationToken)).Url;
}
