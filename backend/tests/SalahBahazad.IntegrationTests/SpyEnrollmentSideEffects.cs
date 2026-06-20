using System.Collections.Concurrent;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Test double for <see cref="IEnrollmentSideEffects"/> that records which enrollments it was asked to
/// provision — lets the redeem/unlock tests prove the <c>EnrollmentCreated</c>/<c>Extended</c> event handler
/// actually ran (the production stub is a silent no-op). Asserting on the recorded id (not a count) keeps it
/// robust to other tests in the shared collection.
/// </summary>
public sealed class SpyEnrollmentSideEffects : IEnrollmentSideEffects
{
    private readonly ConcurrentBag<Guid> _generatedFor = [];

    public IReadOnlyCollection<Guid> GeneratedFor => _generatedFor;

    public Task GenerateAssessmentsAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        _generatedFor.Add(enrollmentId);
        return Task.CompletedTask;
    }
}
