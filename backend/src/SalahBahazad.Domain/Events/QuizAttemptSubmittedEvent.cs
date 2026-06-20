using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a student submits a quiz attempt (FR-PLAT-QZ-007/010). Attributed to the <b>student</b> actor
/// (their act) — one semantic audit row under the calling principal. The grade's attendance write is carried by
/// the separate <see cref="QuizGradedEvent"/>, so this row records only the submission, not a second diff.
/// </summary>
public sealed record QuizAttemptSubmittedEvent(
    Guid UserQuizId, Guid AttemptId, int Number, int ScorePercent) : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "QuizAttemptSubmitted";
    public string AuditSummary => $"Quiz attempt #{Number} submitted: {ScorePercent}%.";
}
