namespace SalahBahazad.Application.Common;

/// <summary>
/// Tunables for the video playback gate, bound from the <c>Playback</c> configuration section. Kept in the
/// Application layer (not Infrastructure) because the gate + redeem handlers consume them. ffmpeg-specific
/// knobs live separately in the Infrastructure transcode options.
/// </summary>
public sealed class PlaybackOptions
{
    public const string SectionName = "Playback";

    /// <summary>TTL of the one-time handoff code (FR-PLAT-VID-005). Short — it is exchanged immediately.</summary>
    public int HandoffTtlSeconds { get; set; } = 60;

    /// <summary>TTL of the per-playback signed segment URLs (FR-PLAT-VID-003). Short to limit replay.</summary>
    public int SegmentUrlTtlSeconds { get; set; } = 120;
}
