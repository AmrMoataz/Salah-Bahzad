namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// ffmpeg/HLS transcode configuration, bound from the <c>Transcode</c> section. ffmpeg is a binary the API's
/// Hangfire worker shells out to (not an Aspire service) — see <c>docs/hls-transcoding-and-streaming.md</c> §6.
/// </summary>
public sealed class TranscodeOptions
{
    public const string SectionName = "Transcode";

    /// <summary>The ffmpeg executable — on PATH in dev, in the API image in prod. Override for a non-PATH install.</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Target HLS segment length in seconds.</summary>
    public int HlsTimeSeconds { get; set; } = 6;
}
