using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// An ordered video within a <see cref="Session"/> (FR-PLAT-SES-002, FR-ADM-SES-003), each with its own
/// per-enrollment <see cref="AccessCount"/> (view cap). A child of the Session aggregate — added, edited,
/// reordered, and removed only through the <see cref="Session"/> root. The source is uploaded to R2 and the
/// DB keeps only the object key / HLS manifest reference (FR-PLAT-VID-007). <see cref="LengthMinutes"/> is
/// admin-entered metadata (real duration comes from the Phase 5 transcode pipeline).
/// </summary>
public sealed class SessionVideo : EntityBase
{
    private SessionVideo() { }

    public Guid SessionId { get; private set; }
    public string Title { get; private set; } = string.Empty;

    /// <summary>Zero-based position within the session's video list (FR-ADM-SES-003 reorder).</summary>
    public int Order { get; private set; }

    /// <summary>Admin-entered run length in minutes shown in the UI (e.g. "8:00").</summary>
    public int LengthMinutes { get; private set; }

    /// <summary>Allowed views per enrollment for this video (FR-PLAT-SES-002).</summary>
    public int AccessCount { get; private set; }

    /// <summary>R2 object key of the uploaded source video (FR-PLAT-VID-007).</summary>
    public string SourceObjectKey { get; private set; } = string.Empty;

    /// <summary>R2 key of the HLS manifest once transcoded; null until <see cref="VideoProcessingStatus.Ready"/>.</summary>
    public string? HlsManifestKey { get; private set; }

    public VideoProcessingStatus ProcessingStatus { get; private set; } = VideoProcessingStatus.Pending;

    internal static SessionVideo Create(
        Guid sessionId, string title, int order, int lengthMinutes, int accessCount, string sourceObjectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceObjectKey);
        Validate(lengthMinutes, accessCount);

        return new SessionVideo
        {
            SessionId = sessionId,
            Title = title.Trim(),
            Order = order,
            LengthMinutes = lengthMinutes,
            AccessCount = accessCount,
            SourceObjectKey = sourceObjectKey,
            ProcessingStatus = VideoProcessingStatus.Pending,
        };
    }

    internal void UpdateMetadata(string title, int lengthMinutes, int accessCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Validate(lengthMinutes, accessCount);

        Title = title.Trim();
        LengthMinutes = lengthMinutes;
        AccessCount = accessCount;
    }

    /// <summary>Replaces the source video; transcode restarts so processing returns to <c>Pending</c>.</summary>
    internal void ReplaceSource(string sourceObjectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceObjectKey);
        SourceObjectKey = sourceObjectKey;
        HlsManifestKey = null;
        ProcessingStatus = VideoProcessingStatus.Pending;
    }

    internal void SetOrder(int order) => Order = order;

    // ── Transcode pipeline transitions (driven by IVideoProcessingQueue, FR-PLAT-VID-001..006) ──
    public void MarkProcessing() => ProcessingStatus = VideoProcessingStatus.Processing;

    public void MarkReady(string? hlsManifestKey = null)
    {
        HlsManifestKey = string.IsNullOrWhiteSpace(hlsManifestKey) ? null : hlsManifestKey;
        ProcessingStatus = VideoProcessingStatus.Ready;
    }

    public void MarkFailed() => ProcessingStatus = VideoProcessingStatus.Failed;

    private static void Validate(int lengthMinutes, int accessCount)
    {
        if (lengthMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(lengthMinutes), "Video length cannot be negative.");
        if (accessCount < 0)
            throw new ArgumentOutOfRangeException(nameof(accessCount), "Access count cannot be negative.");
    }
}
