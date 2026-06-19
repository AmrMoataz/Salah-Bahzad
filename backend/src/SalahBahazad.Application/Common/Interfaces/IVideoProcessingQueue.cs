namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Transcode seam for uploaded session videos (FR-PLAT-VID-001..006). A handler enqueues a freshly
/// uploaded source after persisting the <c>Pending</c> video; the implementation drives it through the
/// HLS pipeline and flips the processing status. In Phase 3 this is stubbed (immediately marks the video
/// <c>Ready</c>); the real Hangfire + HLS + AES-128 pipeline arrives in Phase 5.
/// </summary>
public interface IVideoProcessingQueue
{
    /// <summary>Enqueues transcoding of the given video's uploaded source.</summary>
    Task EnqueueTranscodeAsync(
        Guid videoId, string sourceObjectKey, CancellationToken cancellationToken = default);
}
