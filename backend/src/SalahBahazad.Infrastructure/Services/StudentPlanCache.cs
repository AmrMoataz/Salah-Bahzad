using Microsoft.Extensions.Caching.Hybrid;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// <see cref="IStudentPlanCache"/> over the registered <see cref="HybridCache"/> (contract §D). Each cached
/// weekly plan is tagged <c>plan:{studentId}</c>, so a single <see cref="HybridCache.RemoveByTagAsync(string,
/// CancellationToken)"/> drops all of a student's cached weeks (L1 + L2) on any state change. The writing node
/// clears immediately; other nodes converge within their <c>LocalCacheExpiration</c> (≤ 60 s, §C).
/// </summary>
internal sealed class StudentPlanCache(HybridCache cache) : IStudentPlanCache
{
    public ValueTask InvalidateAsync(Guid studentId, CancellationToken cancellationToken = default)
        => cache.RemoveByTagAsync($"plan:{studentId}", cancellationToken);
}
