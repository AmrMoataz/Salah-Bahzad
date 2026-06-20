using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Review.DTOs;

/// <summary>One option in the staff review — <b>shows correctness</b> (unlike the student §A shape).</summary>
public sealed record ReviewOptionDto(Guid Id, int Order, string Text, bool IsCorrect);

/// <summary>One question in the staff review: the snapshot plus the student's pick and whether it was correct.</summary>
public sealed record ReviewQuestionDto(
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    string? HintUrl,
    IReadOnlyList<ReviewOptionDto> Options,
    Guid? SelectedOptionId,
    bool IsCorrect);

/// <summary>
/// The per-question submitted-vs-correct review of a student's assignment (contract §C #7, FR-ADM-REV-001).
/// Header = scrReview: name, "{session} · Assignment review", Score, Time. Score fields are computed from the
/// current answers, so an in-progress assignment shows its partial standing.
/// </summary>
public sealed record AssignmentReviewDto(
    string? StudentName,
    string? SessionTitle,
    int CorrectCount,
    int QuestionCount,
    int ScoreMarks,
    int MaxMarks,
    int Percent,
    int TimeSpentSeconds,
    AssignmentStatus Status,
    IReadOnlyList<ReviewQuestionDto> Questions);

/// <summary>One row of the in-assessment behaviour timeline (contract §C #8, FR-ADM-REV-003).</summary>
public sealed record BehaviourEventDto(
    AssessmentEventType Type,
    string Label,
    int? QuestionOrder,
    DateTimeOffset OccurredAtUtc);
