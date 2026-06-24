namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Invalidation seam for the student-Home weekly plan cache (contract §D). The plan is a disposable Redis
/// projection keyed per <c>(tenant, student, isoWeek)</c> and tagged <c>plan:{studentId}</c>; dropping that tag
/// invalidates all of a student's cached weeks at once. Implemented in Infrastructure over <c>HybridCache</c>
/// (<c>RemoveByTagAsync</c>). Called from every student state-change — via the existing
/// <c>INotificationHandler</c>s where a domain event exists (enrollment created/extended/refunded, quiz graded,
/// assignment graded) and <b>inline</b> at the two write sites that raise none (the 5C video-playback gate and a
/// non-final assignment answer). A missed drop self-heals at the weekly TTL; this keeps it correct intra-week.
/// </summary>
public interface IStudentPlanCache
{
    /// <summary>Drops every cached weekly plan for <paramref name="studentId"/> (≡ <c>RemoveByTagAsync</c>
    /// <c>"plan:{studentId}"</c>). Idempotent and off the request's critical work (NFR-SCAL-004).</summary>
    ValueTask InvalidateAsync(Guid studentId, CancellationToken cancellationToken = default);
}
