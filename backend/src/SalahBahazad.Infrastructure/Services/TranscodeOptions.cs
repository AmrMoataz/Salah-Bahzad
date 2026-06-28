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

    /// <summary>
    /// x264 encoding preset (trades CPU-seconds for output size). Defaults to <c>veryfast</c> so a single
    /// transcode doesn't monopolise a small (2 vCPU) shared box while the API serves live traffic.
    /// </summary>
    public string VideoPreset { get; set; } = "veryfast";

    /// <summary>
    /// ffmpeg thread cap. Defaults to 1 so one transcode can't saturate every core on a constrained host;
    /// raise it where the box has spare CPU.
    /// </summary>
    public int Threads { get; set; } = 1;
}
