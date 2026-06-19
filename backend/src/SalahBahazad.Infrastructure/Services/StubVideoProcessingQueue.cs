using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Phase 3 stand-in for the video transcode pipeline (<see cref="IVideoProcessingQueue"/>): instead of
/// pushing a Hangfire job, it immediately marks the video <c>Ready</c> so the content flow is exercisable
/// end-to-end without HLS. Runs in the caller's DI scope (shared <see cref="IAppDbContext"/>), so the
/// status flip participates in the command's transaction. Real Hangfire + HLS + AES-128 is Phase 5
/// (FR-PLAT-VID-001..006).
/// </summary>
internal sealed class StubVideoProcessingQueue(
    IAppDbContext db,
    ILogger<StubVideoProcessingQueue> logger) : IVideoProcessingQueue
{
    public async Task EnqueueTranscodeAsync(
        Guid videoId, string sourceObjectKey, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Stub transcode for video {VideoId}: marking Ready (Phase 5 runs the real HLS pipeline).",
            videoId);

        var video = await db.SessionVideos.FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);
        if (video is null)
            return;

        video.MarkReady();
        await db.SaveChangesAsync(cancellationToken);
    }
}
