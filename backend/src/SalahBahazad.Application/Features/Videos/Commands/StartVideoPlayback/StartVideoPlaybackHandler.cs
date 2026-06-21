using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Videos.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Videos.Commands.StartVideoPlayback;

/// <summary>
/// Runs the gate checks in order, each surfacing a specific machine <c>reason</c> (FR-PLAT-VID-006), then
/// decrements the per-video budget, writes the <c>VideoPlaybackStarted</c> audit (Student actor), and issues a
/// one-time handoff code. The video is resolved through the caller's tenant-filtered <see cref="Domain.Entities.Session"/>
/// (IDOR/tenant → 404, NFR-SEC-007). Decrement + audit commit together; the handoff code is minted only after.
/// </summary>
internal sealed class StartVideoPlaybackHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    TimeProvider clock,
    IAuditWriter auditWriter,
    IPlaybackHandoffStore handoffStore,
    PlaybackOptions playbackOptions)
    : IRequestHandler<StartVideoPlaybackCommand, PlaybackHandoffDto>
{
    public async ValueTask<PlaybackHandoffDto> Handle(
        StartVideoPlaybackCommand command, CancellationToken cancellationToken)
    {
        // 1–2. Resolve the video via its tenant-filtered session (cross-tenant/IDOR → 404), then require Ready.
        var video = await db.Sessions
            .AsNoTracking()
            .SelectMany(s => s.Videos)
            .Where(v => v.Id == command.VideoId)
            .Select(v => new { v.Id, v.SessionId, v.Title, v.ProcessingStatus })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Video", command.VideoId);

        if (video.ProcessingStatus != VideoProcessingStatus.Ready)
            throw new ConflictException("This video is still being processed.", "not_ready");

        // 3–5 + decrement + audit, atomically.
        var enrollmentId = await db.ExecuteInTransactionAsync(async () =>
        {
            var enrollment = await db.Enrollments
                .Include(e => e.VideoAccesses)
                .FirstOrDefaultAsync(
                    e => e.SessionId == video.SessionId && e.StudentId == currentUser.UserId, cancellationToken);

            if (enrollment is null || enrollment.Status != EnrollmentStatus.Active)
                throw new ForbiddenException("You are not enrolled in this session.", "not_enrolled");

            var now = clock.GetUtcNow();
            if (enrollment.ExpiresAtUtc is { } expiresAt && expiresAt <= now)
                throw new ForbiddenException("Your enrollment for this session has expired.", "enrollment_expired");

            // A UserQuiz exists for the enrollment only when the session is quiz-gated (5B-2); unpassed → blocked.
            var quiz = await db.UserQuizzes
                .FirstOrDefaultAsync(q => q.EnrollmentId == enrollment.Id, cancellationToken);
            if (quiz is not null && !quiz.Passed)
                throw new ForbiddenException("Pass the prerequisite quiz to unlock this video.", "quiz_required");

            var access = enrollment.VideoAccesses.FirstOrDefault(a => a.VideoId == video.Id);
            if (access is null || access.AccessRemaining <= 0)
                throw new ForbiddenException("You have no views remaining for this video.", "no_views_remaining");

            access.Decrement();
            await db.SaveChangesAsync(cancellationToken);

            // FR-PLAT-VID-002: who watched what, when. Student actor (inferred from the JWT by the writer).
            await auditWriter.WriteAsync(
                new AuditWriteRequest("VideoPlaybackStarted", "SessionVideo", video.Id, $"Watched: {video.Title}"),
                cancellationToken);

            return enrollment.Id;
        }, cancellationToken);

        // Mint the single-use code only after the view is durably spent — a rolled-back transaction must never
        // leave a redeemable code (FR-PLAT-VID-005). The raw token/URL is never returned here.
        var ttl = TimeSpan.FromSeconds(playbackOptions.HandoffTtlSeconds);
        var code = await handoffStore.IssueAsync(
            new PlaybackHandoff(video.Id, enrollmentId, currentUser.UserId, currentUser.TenantId),
            ttl,
            cancellationToken);

        return new PlaybackHandoffDto(code, clock.GetUtcNow().Add(ttl));
    }
}
