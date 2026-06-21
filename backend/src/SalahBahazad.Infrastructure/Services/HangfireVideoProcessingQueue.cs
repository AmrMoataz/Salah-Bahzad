using Hangfire;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Infrastructure.Jobs;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// <see cref="IVideoProcessingQueue"/> over Hangfire (replaces the Phase-3 <c>StubVideoProcessingQueue</c>):
/// enqueues a durable <see cref="VideoTranscodeJob"/> instead of transcoding inline. The current tenant is
/// captured at enqueue time and passed to the job so its no-HTTP scope can attribute work to System within the
/// right tenant. The job re-loads everything else from the DB, so a rolled-back caller transaction simply leaves
/// the job to no-op on a missing video.
/// </summary>
internal sealed class HangfireVideoProcessingQueue(
    IBackgroundJobClient backgroundJobs,
    ICurrentTenantResolver tenantResolver) : IVideoProcessingQueue
{
    public Task EnqueueTranscodeAsync(
        Guid videoId, string sourceObjectKey, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantResolver.TenantId;
        backgroundJobs.Enqueue<VideoTranscodeJob>(job => job.RunAsync(videoId, tenantId));
        return Task.CompletedTask;
    }
}
