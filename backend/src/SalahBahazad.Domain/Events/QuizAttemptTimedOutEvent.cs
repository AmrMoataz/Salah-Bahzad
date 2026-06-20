using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when the authoritative server-side timer auto-submits a quiz attempt at its deadline
/// (FR-PLAT-QZ-005/010) — grading whatever was answered. Attributed to the <b>System</b> actor
/// (<see cref="ISystemActorAuditEvent"/>, FR-PLAT-AUD-005): the platform's background job performs it with no
/// request principal. The grade's attendance write is carried by the separate <see cref="QuizGradedEvent"/>.
/// </summary>
public sealed record QuizAttemptTimedOutEvent(
    Guid UserQuizId, Guid AttemptId, int Number, int ScorePercent) : ISystemActorAuditEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "QuizAttemptTimedOut";
    public string AuditSummary => $"Quiz attempt #{Number} timed out: auto-submitted at {ScorePercent}%.";
}
