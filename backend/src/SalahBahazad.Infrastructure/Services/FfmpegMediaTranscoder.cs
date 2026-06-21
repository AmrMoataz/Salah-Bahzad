using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Interfaces;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// <see cref="IMediaTranscoder"/> over the ffmpeg binary (<see cref="TranscodeOptions.FfmpegPath"/>). Generates a
/// random AES-128 content key + IV, writes the ffmpeg key-info file (baking
/// <see cref="HlsConventions.KeyUriPlaceholder"/> as the manifest's key URI), produces single-rendition VOD HLS,
/// and ffprobes the source for its exact duration. ffmpeg reads the source from the supplied signed URL and
/// writes into <c>outputDirectory</c>, set as its working directory so the manifest lists <b>relative</b> segment names.
/// </summary>
internal sealed class FfmpegMediaTranscoder(TranscodeOptions options, ILogger<FfmpegMediaTranscoder> logger)
    : IMediaTranscoder
{
    public async Task<TranscodeOutput> TranscodeToEncryptedHlsAsync(
        string sourceUrl, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        // 1. AES-128 content key + IV. The raw key is returned for the caller to store as the private key object.
        var key = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(16);
        var keyFilePath = Path.Combine(outputDirectory, HlsConventions.KeyFileName);
        await File.WriteAllBytesAsync(keyFilePath, key, cancellationToken);

        // 2. ffmpeg key-info file: line 1 = the URI written verbatim into the playlist (a placeholder, rewritten
        //    to the gated key endpoint at redeem); line 2 = the local key file ffmpeg reads; line 3 = the IV hex.
        var keyInfoPath = Path.Combine(outputDirectory, "enc.keyinfo");
        await File.WriteAllLinesAsync(
            keyInfoPath,
            [HlsConventions.KeyUriPlaceholder, keyFilePath, Convert.ToHexString(iv).ToLowerInvariant()],
            cancellationToken);

        // 3. ffmpeg → encrypted single-rendition VOD HLS. Relative output names (working dir = outputDirectory)
        //    so the manifest lists "seg_000.ts", not absolute paths.
        var (exit, _, stderr) = await RunAsync(
            options.FfmpegPath,
            [
                "-nostdin", "-y",
                "-i", sourceUrl,
                "-c:v", "libx264", "-c:a", "aac",
                "-hls_time", options.HlsTimeSeconds.ToString(CultureInfo.InvariantCulture),
                "-hls_playlist_type", "vod",
                "-hls_key_info_file", keyInfoPath,
                "-hls_segment_filename", "seg_%03d.ts",
                HlsConventions.ManifestFileName,
            ],
            outputDirectory,
            cancellationToken);

        if (exit != 0)
        {
            logger.LogError("ffmpeg exited {ExitCode}. stderr tail: {Stderr}", exit, Tail(stderr));
            throw new InvalidOperationException($"ffmpeg failed with exit code {exit}.");
        }

        var manifestPath = Path.Combine(outputDirectory, HlsConventions.ManifestFileName);
        var segmentPaths = Directory.GetFiles(outputDirectory, "seg_*.ts").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (segmentPaths.Length == 0 || !File.Exists(manifestPath))
            throw new InvalidOperationException("ffmpeg produced no HLS output.");

        var durationSeconds = await ProbeDurationSecondsAsync(sourceUrl, cancellationToken);

        return new TranscodeOutput(manifestPath, key, segmentPaths, durationSeconds);
    }

    /// <summary>ffprobes the source for its exact duration (seconds, rounded). Returns 0 when unavailable so a
    /// probe failure never fails the transcode — the length simply stays unset.</summary>
    private async Task<int> ProbeDurationSecondsAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        try
        {
            var (exit, stdout, _) = await RunAsync(
                FfprobePath(options.FfmpegPath),
                ["-v", "error", "-show_entries", "format=duration",
                 "-of", "default=nokey=1:noprint_wrappers=1", sourceUrl],
                workingDirectory: null,
                cancellationToken);

            if (exit == 0 &&
                double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
                seconds > 0)
            {
                return (int)Math.Round(seconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ffprobe could not determine the video duration; leaving it unset.");
        }

        return 0;
    }

    /// <summary>ffprobe lives beside ffmpeg: "ffmpeg" → "ffprobe", "…/ffmpeg.exe" → "…/ffprobe.exe".</summary>
    private static string FfprobePath(string ffmpegPath)
    {
        var probe = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        var directory = Path.GetDirectoryName(ffmpegPath);
        return string.IsNullOrEmpty(directory) ? probe : Path.Combine(directory, probe);
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName, string[] args, string? workingDirectory, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrEmpty(workingDirectory))
            psi.WorkingDirectory = workingDirectory;
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start '{fileName}'. Is ffmpeg installed and on PATH? See " +
                "docs/hls-transcoding-and-streaming.md §6.", ex);
        }

        // Drain both pipes concurrently with the wait to avoid a full-buffer deadlock.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string Tail(string text, int max = 2000)
        => text.Length <= max ? text : text[^max..];
}
