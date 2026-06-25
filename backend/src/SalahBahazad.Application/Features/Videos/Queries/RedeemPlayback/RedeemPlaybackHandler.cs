using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Videos.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Videos.Queries.RedeemPlayback;

/// <summary>
/// Consumes the single-use handoff code (the proof of a prior successful gate, so no re-authorisation here),
/// reads the stored relative manifest, and rewrites it for this playback: every segment URI → a fresh
/// short-lived signed R2 URL, and the <c>#EXT-X-KEY</c> placeholder → the absolute gated key endpoint. The
/// rewritten manifest is returned inline so nothing durable is hotlinkable (FR-PLAT-VID-003).
/// </summary>
internal sealed class RedeemPlaybackHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    IPlaybackHandoffStore handoffStore,
    IFileStorage fileStorage,
    TimeProvider clock,
    PlaybackOptions playbackOptions)
    : IRequestHandler<RedeemPlaybackQuery, PlaybackManifestDto>
{
    public async ValueTask<PlaybackManifestDto> Handle(
        RedeemPlaybackQuery query, CancellationToken cancellationToken)
    {
        var handoff = await handoffStore.ConsumeAsync(query.HandoffCode, cancellationToken);
        if (handoff is null || handoff.StudentId != currentUser.UserId)
            throw new GoneException("This playback link has expired. Please start playback again.", "handoff_expired");

        var video = await db.Sessions
            .AsNoTracking()
            .SelectMany(s => s.Videos)
            .Where(v => v.Id == handoff.VideoId)
            .Select(v => new { v.HlsManifestKey, v.ProcessingStatus })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Video", handoff.VideoId);

        if (video.ProcessingStatus != VideoProcessingStatus.Ready || string.IsNullOrEmpty(video.HlsManifestKey))
            throw new ConflictException("This video is still being processed.", "not_ready");

        var manifestText = await ReadManifestAsync(video.HlsManifestKey, cancellationToken);

        // Sign each segment URI freshly; the manifest dies with the soonest segment TTL.
        var ttl = TimeSpan.FromSeconds(playbackOptions.SegmentUrlTtlSeconds);
        var soonestExpiry = clock.GetUtcNow().Add(ttl);
        var lines = manifestText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
                continue; // an HLS tag or blank line — only bare URI lines are segments

            var segmentKey = HlsConventions.SegmentKey(video.HlsManifestKey, line);
            var signed = await fileStorage.GetSignedReadUrlAsync(segmentKey, ttl, cancellationToken);
            lines[i] = signed.Url;
            if (signed.ExpiresAtUtc < soonestExpiry)
                soonestExpiry = signed.ExpiresAtUtc;
        }

        var keyUrl = $"{query.ApiBaseUrl}/api/me/videos/{handoff.VideoId}/hls.key";
        var rewritten = string.Join('\n', lines).Replace(HlsConventions.KeyUriPlaceholder, keyUrl);

        // The per-video view budget for this enrollment, so the player can show "N of M views left"
        // (FR-APP-VID-004). The gate (D1) already spent this view, so AccessRemaining is the post-Play count.
        // The handoff implies a passed gate, so the access row exists; default defensively if it somehow doesn't.
        var access = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == handoff.EnrollmentId)
            .SelectMany(e => e.VideoAccesses)
            .Where(a => a.VideoId == handoff.VideoId)
            .Select(a => new { a.AccessRemaining, a.AccessAllowed })
            .FirstOrDefaultAsync(cancellationToken);

        return new PlaybackManifestDto(
            rewritten, keyUrl, soonestExpiry, access?.AccessRemaining ?? 0, access?.AccessAllowed ?? 0);
    }

    private async Task<string> ReadManifestAsync(string manifestKey, CancellationToken cancellationToken)
    {
        await using var stream = await fileStorage.OpenReadAsync(manifestKey, cancellationToken);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
