using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Infrastructure.Jobs;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Test double for <see cref="IVideoProcessingQueue"/> that runs the <b>real</b> <see cref="VideoTranscodeJob"/>
/// inline (synchronously, with the faked transcoder) instead of enqueuing to Hangfire. This keeps the content
/// tests deterministic — the video is <c>Ready</c> by the time the upload request returns, exactly as the
/// Phase-3 stub behaved — while still exercising the genuine job (upload to MinIO + <c>MarkReady</c>). The real
/// Hangfire enqueue path is proven in live wiring.
/// </summary>
internal sealed class InlineVideoProcessingQueue(VideoTranscodeJob job, ICurrentTenantResolver tenantResolver)
    : IVideoProcessingQueue
{
    public Task EnqueueTranscodeAsync(
        Guid videoId, string sourceObjectKey, CancellationToken cancellationToken = default)
        => job.RunAsync(videoId, tenantResolver.TenantId);
}
