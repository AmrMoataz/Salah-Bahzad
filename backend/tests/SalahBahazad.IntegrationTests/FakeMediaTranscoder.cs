using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Test double for <see cref="IMediaTranscoder"/> — writes a tiny but valid encrypted-HLS output (a manifest
/// with the key-URI placeholder + one segment + a deterministic 16-byte key) without invoking ffmpeg, so the
/// transcode job's upload → <c>MarkReady</c> → playback path is exercised in CI without the binary. Real ffmpeg
/// is proven in live wiring (and the opt-in real-ffmpeg test).
/// </summary>
internal sealed class FakeMediaTranscoder : IMediaTranscoder
{
    public static byte[] Key { get; } = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

    /// <summary>The duration this fake reports, so tests can assert the computed length.</summary>
    public const int DurationSeconds = 2;

    public async Task<TranscodeOutput> TranscodeToEncryptedHlsAsync(
        string sourceUrl, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var manifestPath = Path.Combine(outputDirectory, HlsConventions.ManifestFileName);
        var segmentPath = Path.Combine(outputDirectory, "seg_000.ts");

        await File.WriteAllTextAsync(manifestPath, BuildManifest(), cancellationToken);
        await File.WriteAllBytesAsync(segmentPath, [1, 2, 3, 4, 5, 6, 7, 8], cancellationToken);

        return new TranscodeOutput(manifestPath, Key, [segmentPath], DurationSeconds);
    }

    public static string BuildManifest() => string.Join('\n',
        "#EXTM3U",
        "#EXT-X-VERSION:3",
        "#EXT-X-TARGETDURATION:6",
        "#EXT-X-MEDIA-SEQUENCE:0",
        "#EXT-X-PLAYLIST-TYPE:VOD",
        $"#EXT-X-KEY:METHOD=AES-128,URI=\"{HlsConventions.KeyUriPlaceholder}\",IV=0x{new string('0', 32)}",
        "#EXTINF:6.000000,",
        "seg_000.ts",
        "#EXT-X-ENDLIST",
        "");
}
