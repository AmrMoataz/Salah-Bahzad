using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a quiz attempt is forfeited because the single-sitting connection was lost (page close /
/// disconnect, FR-PLAT-QZ-004/010) — scored 0 and consuming the attempt. Attributed to the <b>System</b> actor
/// (<see cref="ISystemActorAuditEvent"/>, FR-PLAT-AUD-005): the platform enforces the forfeit, often from the
/// hub's <c>OnDisconnectedAsync</c> with no request principal. No <see cref="QuizGradedEvent"/> follows — a 0
/// never improves the best-of, so attendance is unchanged.
/// </summary>
public sealed record QuizAttemptForfeitedEvent(
    Guid UserQuizId, Guid AttemptId, int Number) : ISystemActorAuditEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "QuizAttemptForfeited";
    public string AuditSummary => $"Quiz attempt #{Number} forfeited (connection lost): scored 0.";
}
