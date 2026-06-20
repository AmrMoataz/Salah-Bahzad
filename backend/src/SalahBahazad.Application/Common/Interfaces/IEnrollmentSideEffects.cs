namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Seam for the post-enrollment assessment provisioning (FR-PLAT-ENR-005): generating the per-student
/// assignment snapshot (FR-PLAT-ASG-001) and the prerequisite-quiz snapshot (FR-PLAT-QZ-001). Driven by the
/// <c>EnrollmentCreated</c>/<c>EnrollmentExtended</c> domain events so a redeem (#12) and an unlock (#9)
/// trigger it identically. <b>Stubbed in Phase 4</b> (logs intent only); the engines that consume the
/// snapshots land in Phase 5 — mirrors how Phase 3 stubbed <see cref="IVideoProcessingQueue"/>.
/// </summary>
public interface IEnrollmentSideEffects
{
    Task GenerateAssessmentsAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
}
