using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Jobs;

/// <summary>
/// The Hangfire job that turns an uploaded source video into AES-128-encrypted HLS (FR-PLAT-VID-003/007),
/// replacing the Phase-3 stub. It has no HTTP request, so it opens an <see cref="ISystemOperationContext"/> scope
/// for <paramref name="tenantId"/> — the EF global filter then scopes loads and the audit interceptor attributes
/// the status transitions to System. ffmpeg reads the source straight from R2 via a signed URL (no multi-GB
/// download to the app server's disk); outputs are uploaded to R2 and the DB stores only the manifest + key
/// object keys. Public so Hangfire's DI activator can resolve and invoke it.
/// </summary>
public sealed class VideoTranscodeJob(
    IAppDbContext db,
    IFileStorage fileStorage,
    IMediaTranscoder transcoder,
    ISystemOperationContext systemOperation,
    ILogger<VideoTranscodeJob> logger)
{
    public async Task RunAsync(Guid videoId, Guid tenantId)
    {
        using var scope = systemOperation.Begin(tenantId);
        var cancellationToken = CancellationToken.None;

        var video = await db.SessionVideos.FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);
        if (video is null)
        {
            logger.LogWarning("Transcode skipped: video {VideoId} no longer exists.", videoId);
            return;
        }

        var workingDirectory = Path.Combine(Path.GetTempPath(), $"hls-{videoId:n}");
        try
        {
            video.MarkProcessing();
            await db.SaveChangesAsync(cancellationToken);

            // ffmpeg reads the source from a signed URL — generous TTL for a long encode; nothing hits app disk.
            var sourceUrl = (await fileStorage.GetSignedReadUrlAsync(
                video.SourceObjectKey, TimeSpan.FromHours(6), cancellationToken)).Url;

            var output = await transcoder.TranscodeToEncryptedHlsAsync(
                sourceUrl, workingDirectory, cancellationToken);

            var hlsPrefix = HlsConventions.HlsPrefix(video.SourceObjectKey, videoId);
            var manifestKey = HlsConventions.ManifestKey(hlsPrefix);
            var keyObjectKey = HlsConventions.KeyObjectKey(hlsPrefix);

            await UploadFileAsync(output.ManifestFilePath, manifestKey, "application/vnd.apple.mpegurl", cancellationToken);
            foreach (var segmentPath in output.SegmentFilePaths)
            {
                var segmentKey = HlsConventions.SegmentKey(manifestKey, Path.GetFileName(segmentPath));
                await UploadFileAsync(segmentPath, segmentKey, "video/mp2t", cancellationToken);
            }
            await UploadBytesAsync(output.KeyBytes, keyObjectKey, "application/octet-stream", cancellationToken);

            video.MarkReady(manifestKey, keyObjectKey, output.DurationSeconds);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Transcoded video {VideoId} → encrypted HLS ({SegmentCount} segments, {DurationSeconds}s).",
                videoId, output.SegmentFilePaths.Count, output.DurationSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transcode failed for video {VideoId}.", videoId);
            video.MarkFailed();
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Could not persist Failed status for video {VideoId}.", videoId);
            }

            throw; // surface to Hangfire for its retry/visibility policy
        }
        finally
        {
            TryDeleteDirectory(workingDirectory);
        }
    }

    private async Task UploadFileAsync(string path, string key, string contentType, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        await fileStorage.UploadPrivateAsync(key, stream, contentType, cancellationToken);
    }

    private async Task UploadBytesAsync(byte[] bytes, string key, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes);
        await fileStorage.UploadPrivateAsync(key, stream, contentType, cancellationToken);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Best-effort scratch cleanup; a leftover temp dir must never fail the transcode.
        }
    }
}
