using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// Per-enrollment view budget for one session video (FR-PLAT-ENR-005, FR-PLAT-VID-001). Provisioned from the
/// video's <see cref="SessionVideo.AccessCount"/> when the enrollment is granted; the remaining count is
/// <i>spent</i> by the Phase 5 playback gate. A child of the <see cref="Enrollment"/> aggregate.
/// <see cref="IAuditViaEventOnly"/> — provisioning is covered by the enrollment's semantic event, so it adds
/// no audit row of its own.
/// </summary>
public sealed class EnrollmentVideoAccess : EntityBase, IAuditViaEventOnly
{
    private EnrollmentVideoAccess() { }

    public Guid EnrollmentId { get; private set; }
    public Guid VideoId { get; private set; }

    /// <summary>The budget granted (the video's access count at enrol/extend time).</summary>
    public int AccessAllowed { get; private set; }

    /// <summary>Views still available; decremented by the Phase 5 playback gate.</summary>
    public int AccessRemaining { get; private set; }

    internal static EnrollmentVideoAccess Create(Guid enrollmentId, Guid videoId, int accessCount)
        => new()
        {
            EnrollmentId = enrollmentId,
            VideoId = videoId,
            AccessAllowed = accessCount,
            AccessRemaining = accessCount,
        };

    /// <summary>Resets the budget on re-enroll/extend (FR-PLAT-ENR-004) — does not create a second row.</summary>
    internal void ResetTo(int accessCount)
    {
        AccessAllowed = accessCount;
        AccessRemaining = accessCount;
    }

    /// <summary>
    /// Spends one view at the playback gate (FR-PLAT-VID-002). The gate checks <see cref="AccessRemaining"/> first
    /// and surfaces a <c>no_views_remaining</c> reason; this guard is the last-line domain invariant
    /// (<see cref="InvalidOperationException"/> if the budget is already exhausted).
    /// </summary>
    public void Decrement()
    {
        if (AccessRemaining <= 0)
            throw new InvalidOperationException("No views remaining for this video.");
        AccessRemaining--;
    }
}
