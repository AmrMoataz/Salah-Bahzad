namespace SalahBahazad.Application.Common;

/// <summary>
/// The HLS storage + manifest conventions shared between the transcode job (which writes them) and the
/// playback redeem handler (which reads + rewrites them). Renditions live in R2 as siblings of the source
/// under <c>…/videos/hls/{videoId}/</c>: an <c>index.m3u8</c> manifest, AES-128 <c>seg_*.ts</c> segments,
/// and the private <c>enc.key</c> object. The DB stores only the manifest + key object keys
/// (FR-PLAT-VID-007); segment keys are derived from the manifest's directory, so the layout can evolve
/// without a migration.
/// </summary>
public static class HlsConventions
{
    /// <summary>
    /// Baked into the stored manifest's <c>#EXT-X-KEY URI=""</c> at transcode time; replaced with the
    /// absolute, gated key-endpoint URL at redeem (so the stored manifest hard-codes no environment URL).
    /// </summary>
    public const string KeyUriPlaceholder = "__HLS_KEY_URI__";

    public const string ManifestFileName = "index.m3u8";
    public const string KeyFileName = "enc.key";

    /// <summary>
    /// The R2 prefix (with a trailing slash) the HLS renditions for a video live under — a sibling of the
    /// source object, e.g. <c>sessions/{t}/{s}/videos/hls/{videoId}/</c>.
    /// </summary>
    public static string HlsPrefix(string sourceObjectKey, Guid videoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceObjectKey);
        var slash = sourceObjectKey.LastIndexOf('/');
        var directory = slash >= 0 ? sourceObjectKey[..(slash + 1)] : string.Empty;
        return $"{directory}hls/{videoId:n}/";
    }

    public static string ManifestKey(string hlsPrefix) => hlsPrefix + ManifestFileName;

    public static string KeyObjectKey(string hlsPrefix) => hlsPrefix + KeyFileName;

    /// <summary>The R2 key of a segment named in the manifest, resolved against the manifest's directory.</summary>
    public static string SegmentKey(string manifestKey, string segmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestKey);
        var slash = manifestKey.LastIndexOf('/');
        var directory = slash >= 0 ? manifestKey[..(slash + 1)] : string.Empty;
        return directory + segmentName;
    }
}
