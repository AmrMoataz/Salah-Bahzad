using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// An ordered video within a <see cref="Session"/> (FR-PLAT-SES-002, FR-ADM-SES-003), each with its own
/// per-enrollment <see cref="AccessCount"/> (view cap). A child of the Session aggregate — added, edited,
/// reordered, and removed only through the <see cref="Session"/> root. The source is uploaded to R2 and the
/// DB keeps only the object key / HLS manifest reference (FR-PLAT-VID-007). <see cref="LengthSeconds"/> is
/// computed by the transcode pipeline (ffprobe) — not admin-entered.
/// </summary>
public sealed class SessionVideo : EntityBase
{
    private SessionVideo() { }

    public Guid SessionId { get; private set; }
    public string Title { get; private set; } = string.Empty;

    /// <summary>Zero-based position within the session's video list (FR-ADM-SES-003 reorder).</summary>
    public int Order { get; private set; }

    /// <summary>Run length in seconds — computed by the transcode pipeline (ffprobe) and set on
    /// <see cref="MarkReady"/>; 0 until <see cref="VideoProcessingStatus.Ready"/>. Displayed as MM:SS.</summary>
    public int LengthSeconds { get; private set; }

    /// <summary>Allowed views per enrollment for this video (FR-PLAT-SES-002).</summary>
    public int AccessCount { get; private set; }

    /// <summary>R2 object key of the uploaded source video (FR-PLAT-VID-007).</summary>
    public string SourceObjectKey { get; private set; } = string.Empty;

    /// <summary>R2 key of the HLS manifest once transcoded; null until <see cref="VideoProcessingStatus.Ready"/>.</summary>
    public string? HlsManifestKey { get; private set; }

    /// <summary>R2 key of the AES-128 content-key object once transcoded; null until
    /// <see cref="VideoProcessingStatus.Ready"/> (FR-PLAT-VID-003). Served only by the gated key endpoint —
    /// never public, never embedded in the manifest.</summary>
    public string? HlsKeyObjectKey { get; private set; }

    public VideoProcessingStatus ProcessingStatus { get; private set; } = VideoProcessingStatus.Pending;

    internal static SessionVideo Create(
        Guid sessionId, string title, int order, int accessCount, string sourceObjectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceObjectKey);
        Validate(accessCount);

        return new SessionVideo
        {
            SessionId = sessionId,
            Title = title.Trim(),
            Order = order,
            LengthSeconds = 0, // computed by the transcode pipeline once Ready
            AccessCount = accessCount,
            SourceObjectKey = sourceObjectKey,
            ProcessingStatus = VideoProcessingStatus.Pending,
        };
    }

    internal void UpdateMetadata(string title, int accessCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Validate(accessCount);

        Title = title.Trim();
        AccessCount = accessCount;
    }

    /// <summary>Replaces the source video; transcode restarts so processing returns to <c>Pending</c>.</summary>
    internal void ReplaceSource(string sourceObjectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceObjectKey);
        SourceObjectKey = sourceObjectKey;
        HlsManifestKey = null;
        HlsKeyObjectKey = null;
        ProcessingStatus = VideoProcessingStatus.Pending;
    }

    internal void SetOrder(int order) => Order = order;

    // ── Transcode pipeline transitions (driven by IVideoProcessingQueue, FR-PLAT-VID-001..006) ──
    public void MarkProcessing() => ProcessingStatus = VideoProcessingStatus.Processing;

    /// <summary>Marks the video ready with its HLS keys and the ffprobe-computed duration (seconds).</summary>
    public void MarkReady(string? hlsManifestKey = null, string? hlsKeyObjectKey = null, int durationSeconds = 0)
    {
        HlsManifestKey = string.IsNullOrWhiteSpace(hlsManifestKey) ? null : hlsManifestKey;
        HlsKeyObjectKey = string.IsNullOrWhiteSpace(hlsKeyObjectKey) ? null : hlsKeyObjectKey;
        if (durationSeconds > 0)
            LengthSeconds = durationSeconds;
        ProcessingStatus = VideoProcessingStatus.Ready;
    }

    public void MarkFailed() => ProcessingStatus = VideoProcessingStatus.Failed;

    private static void Validate(int accessCount)
    {
        if (accessCount < 0)
            throw new ArgumentOutOfRangeException(nameof(accessCount), "Access count cannot be negative.");
    }
}
