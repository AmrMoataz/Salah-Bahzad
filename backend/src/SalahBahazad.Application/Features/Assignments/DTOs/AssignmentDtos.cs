using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;
using UserAssignmentEntity = SalahBahazad.Domain.Entities.UserAssignment;

namespace SalahBahazad.Application.Features.Assignments.DTOs;

/// <summary>One MCQ option as shown to the student — no correctness leaked (contract §A).</summary>
public sealed record StudentAssignmentOptionDto(Guid Id, int Order, string Text);

/// <summary>One snapshotted question as shown to the student. <c>imageUrl</c> is a short-lived signed URL;
/// <c>hintUrl</c> is shown only in assignments (FR-PLAT-QB-005). No <c>isCorrect</c> is exposed.</summary>
public sealed record StudentAssignmentQuestionDto(
    Guid Id,
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    string? HintUrl,
    IReadOnlyList<StudentAssignmentOptionDto> Options,
    Guid? SelectedOptionId);

/// <summary>The student's open-book assignment for a session (contract §A #1).</summary>
public sealed record StudentAssignmentDto(
    Guid Id,
    Guid SessionId,
    AssignmentStatus Status,
    int TimeSpentSeconds,
    IReadOnlyList<StudentAssignmentQuestionDto> Questions);

/// <summary>Progress after recording an answer (contract §A #2).</summary>
public sealed record AssignmentProgressDto(int AnsweredCount, int QuestionCount, AssignmentStatus Status);

/// <summary>One MCQ option in the <b>answer-key review</b> — <c>isCorrect</c> is exposed (contract §B.1). This is
/// the only student surface that reveals correctness, and only for the caller's own <c>Completed</c> assignment;
/// the runner's <see cref="StudentAssignmentOptionDto"/> stays correctness-free (5B-1 invariant).</summary>
public sealed record StudentReviewOptionDto(Guid Id, int Order, string Text, bool IsCorrect);

/// <summary>One question in the answer-key review: the snapshot plus the student's pick, each option's correctness,
/// and whether the picked option was the correct one (contract §B.1).</summary>
public sealed record StudentReviewQuestionDto(
    Guid Id,
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    string? HintUrl,
    IReadOnlyList<StudentReviewOptionDto> Options,
    Guid? SelectedOptionId,
    bool IsCorrect);

/// <summary>
/// The caller's own <c>Completed</c> assignment with the answer key and score (contract §B.1, FR-STU-ASG-007).
/// Mirrors the staff <c>AssignmentReviewDto</c> minus <c>studentName</c> (it's the caller) plus
/// <c>id</c>/<c>sessionId</c>/<c>completedAtUtc</c> — a <b>distinct</b> DTO so the runner's correctness-free
/// <see cref="StudentAssignmentDto"/> is never widened.
/// </summary>
public sealed record StudentAssignmentReviewDto(
    Guid Id,
    Guid SessionId,
    string? SessionTitle,
    AssignmentStatus Status,
    int CorrectCount,
    int QuestionCount,
    int ScoreMarks,
    int MaxMarks,
    int Percent,
    int TimeSpentSeconds,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<StudentReviewQuestionDto> Questions);

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md). Image keys are signed on read;
/// the student shape never carries option correctness.</summary>
public static class StudentAssignmentMappings
{
    public static async Task<StudentAssignmentDto> ToStudentDtoAsync(
        this UserAssignmentEntity assignment, IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var questions = new List<StudentAssignmentQuestionDto>();
        foreach (var q in assignment.Questions.OrderBy(q => q.Order))
        {
            var imageUrl = await SignAsync(q.ImageObjectKey, fileStorage, cancellationToken);
            questions.Add(new StudentAssignmentQuestionDto(
                q.Id,
                q.Order,
                q.BodyLatex,
                imageUrl,
                q.HintUrl,
                [.. q.Options.OrderBy(o => o.Order).Select(o => new StudentAssignmentOptionDto(o.Id, o.Order, o.Text))],
                q.SelectedOptionId));
        }

        return new StudentAssignmentDto(
            assignment.Id, assignment.SessionId, assignment.Status, assignment.TimeSpentSeconds, questions);
    }

    public static AssignmentProgressDto ToProgressDto(this UserAssignmentEntity assignment)
        => new(assignment.AnsweredCount, assignment.QuestionCount, assignment.Status);

    /// <summary>Maps a <b>Completed</b> assignment to the answer-key review (contract §B.1) — the only student
    /// projection that exposes option correctness. The caller has already gated <c>Status == Completed</c> and
    /// resolved <paramref name="sessionTitle"/> (via <c>IgnoreQueryFilters</c>, mirroring the staff review);
    /// score fields come from the sealed root (non-null once completed). Questions/options ordered by <c>Order</c>.</summary>
    public static async Task<StudentAssignmentReviewDto> ToReviewDtoAsync(
        this UserAssignmentEntity assignment,
        string? sessionTitle,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var questions = new List<StudentReviewQuestionDto>();
        foreach (var q in assignment.Questions.OrderBy(q => q.Order))
        {
            var imageUrl = await SignAsync(q.ImageObjectKey, fileStorage, cancellationToken);
            questions.Add(new StudentReviewQuestionDto(
                q.Id,
                q.Order,
                q.BodyLatex,
                imageUrl,
                q.Mark,
                q.HintUrl,
                [.. q.Options.OrderBy(o => o.Order).Select(o => new StudentReviewOptionDto(o.Id, o.Order, o.Text, o.IsCorrect))],
                q.SelectedOptionId,
                q.IsCorrect));
        }

        // The Completed gate guarantees the sealed root carries its score; ?? 0 is defensive only.
        return new StudentAssignmentReviewDto(
            assignment.Id,
            assignment.SessionId,
            sessionTitle,
            assignment.Status,
            assignment.CorrectCount ?? 0,
            assignment.QuestionCount,
            assignment.ScoreMarks ?? 0,
            assignment.MaxMarks,
            assignment.Percent,
            assignment.TimeSpentSeconds,
            assignment.CompletedAtUtc ?? default,
            questions);
    }

    private static async Task<string?> SignAsync(
        string? objectKey, IFileStorage fileStorage, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(objectKey)
            ? null
            : (await fileStorage.GetSignedReadUrlAsync(objectKey, cancellationToken: cancellationToken)).Url;
}
