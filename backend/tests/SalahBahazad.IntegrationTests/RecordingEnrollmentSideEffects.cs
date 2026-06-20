using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Infrastructure.Services;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Test decorator that records each enrollment it provisions (into the shared <see cref="SpyEnrollmentSideEffects"/>)
/// and then runs the <b>real</b> <see cref="EnrollmentSideEffects"/>, so Phase-5B-1 assignment generation actually
/// happens during integration tests while the existing "the event fired" assertion keeps working. Scoped so the
/// inner real service resolves the request-scoped <c>IAppDbContext</c> (and its tenant + audit context).
/// </summary>
internal sealed class RecordingEnrollmentSideEffects(
    EnrollmentSideEffects inner, SpyEnrollmentSideEffects recorder) : IEnrollmentSideEffects
{
    public async Task GenerateAssessmentsAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        await recorder.GenerateAssessmentsAsync(enrollmentId, cancellationToken);
        await inner.GenerateAssessmentsAsync(enrollmentId, cancellationToken);
    }
}
