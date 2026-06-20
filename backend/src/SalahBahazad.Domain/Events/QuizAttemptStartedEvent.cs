using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Events;

/// <summary>
/// Raised when a student starts a quiz attempt (FR-PLAT-QZ-003/010). Attributed to the <b>student</b> actor —
/// it is their deliberate act — so it writes one semantic audit row under the calling principal (the default
/// actor; not <see cref="ISystemActorAuditEvent"/>). Pairs with the System-attributed forfeit/timeout events.
/// </summary>
public sealed record QuizAttemptStartedEvent(
    Guid UserQuizId, Guid AttemptId, int Number, Guid StudentId, Guid TenantId, DateTimeOffset DeadlineUtc)
    : IAuditableDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    public string AuditAction => "QuizAttemptStarted";
    public string AuditSummary => $"Quiz attempt #{Number} started.";
}
