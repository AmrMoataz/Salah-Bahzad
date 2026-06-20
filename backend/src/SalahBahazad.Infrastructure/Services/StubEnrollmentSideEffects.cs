using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Phase 4 stand-in for the post-enrollment assessment provisioning (<see cref="IEnrollmentSideEffects"/>):
/// logs intent and no-ops, so the enrollment flow is exercisable end-to-end without the assignment/quiz
/// engines. The real snapshot generation (FR-PLAT-ASG-001, FR-PLAT-QZ-001) arrives in Phase 5 — mirrors
/// <see cref="StubVideoProcessingQueue"/>.
/// </summary>
internal sealed class StubEnrollmentSideEffects(ILogger<StubEnrollmentSideEffects> logger)
    : IEnrollmentSideEffects
{
    public Task GenerateAssessmentsAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Stub enrollment side-effects for {EnrollmentId}: assignment + prerequisite-quiz snapshot " +
            "generation runs in Phase 5.",
            enrollmentId);
        return Task.CompletedTask;
    }
}
