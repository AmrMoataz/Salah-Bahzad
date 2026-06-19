namespace SalahBahazad.Domain.Enums;

/// <summary>
/// Transcode pipeline state of a <see cref="Entities.SessionVideo"/> (FR-PLAT-VID-001..006).
/// A freshly uploaded source is <see cref="Pending"/>; the transcode seam moves it through
/// <see cref="Processing"/> to <see cref="Ready"/> (HLS available) or <see cref="Failed"/>.
/// In Phase 3 the pipeline is stubbed (<c>IVideoProcessingQueue</c>) — real HLS is Phase 5.
/// </summary>
public enum VideoProcessingStatus
{
    /// <summary>Source uploaded; transcode not started.</summary>
    Pending = 0,

    /// <summary>Transcode in progress.</summary>
    Processing = 1,

    /// <summary>HLS rendition ready for delivery.</summary>
    Ready = 2,

    /// <summary>Transcode failed; needs re-upload or retry.</summary>
    Failed = 3,
}
