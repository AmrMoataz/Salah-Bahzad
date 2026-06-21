namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Isolates the ffmpeg invocation that turns a source video into AES-128-encrypted HLS (FR-PLAT-VID-003), so
/// the transcode job can own R2 + the database while tests fake the encode. The implementation generates the
/// random 16-byte content key + IV, writes the ffmpeg key-info file (baking
/// <see cref="Common.HlsConventions.KeyUriPlaceholder"/> as the manifest's key URI), and runs ffmpeg.
/// </summary>
public interface IMediaTranscoder
{
    /// <summary>
    /// Transcodes the media at <paramref name="sourceUrl"/> (a signed R2 GET URL — ffmpeg reads it directly, no
    /// multi-GB local download) into encrypted HLS written under <paramref name="outputDirectory"/>. Returns the
    /// produced manifest path, the raw key bytes (for the caller to store as the private key object), and the
    /// segment file paths. Throws on a non-zero ffmpeg exit.
    /// </summary>
    Task<TranscodeOutput> TranscodeToEncryptedHlsAsync(
        string sourceUrl, string outputDirectory, CancellationToken cancellationToken = default);
}

/// <summary>The local outputs of one transcode, before the job uploads them to R2.
/// <paramref name="DurationSeconds"/> is the ffprobe-measured run length (0 when it could not be determined).</summary>
public sealed record TranscodeOutput(
    string ManifestFilePath, byte[] KeyBytes, IReadOnlyList<string> SegmentFilePaths, int DurationSeconds);
