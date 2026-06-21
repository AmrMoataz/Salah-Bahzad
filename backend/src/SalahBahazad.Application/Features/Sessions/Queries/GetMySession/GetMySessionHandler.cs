using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMySession;

/// <summary>
/// Loads one enrolled session's detail (contract §B/§E). The caller's non-refunded enrollment is resolved first —
/// a missing one is the §B.2 404 (the global filter already excludes other tenants/soft-deleted, so a cross-tenant
/// id is naturally null). Mirrors <c>SessionDetailLoader</c> for the thumbnail signed URL, the video/material
/// ordering, and the grade/subject/spec name resolution with <c>IgnoreQueryFilters</c>; adds the per-video
/// <c>lockState</c> (§E.3), the gate banner (§E.4 — the same <c>UserQuiz</c> predicate the 5C gate reads), and the
/// assignment/quiz status projections. Progress derivation matches the attendance projector exactly (§E.1).
/// </summary>
internal sealed class GetMySessionHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock)
    : IRequestHandler<GetMySessionQuery, MySessionDetailDto>
{
    public async ValueTask<MySessionDetailDto> Handle(
        GetMySessionQuery query, CancellationToken cancellationToken)
    {
        var studentId = currentUser.UserId;
        var now = clock.GetUtcNow();

        // Ownership = the caller's own non-refunded enrollment for this session (§B.2 404 otherwise).
        var enrollment = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.VideoAccesses)
            .FirstOrDefaultAsync(
                e => e.StudentId == studentId
                     && e.SessionId == query.SessionId
                     && e.Status != EnrollmentStatus.Refunded,
                cancellationToken)
            ?? throw new NotFoundException("Session", query.SessionId);

        var session = await db.Sessions
            .AsNoTracking()
            .Include(s => s.Videos)
            .Include(s => s.Materials)
            .FirstOrDefaultAsync(s => s.Id == query.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", query.SessionId);

        // Display names — IgnoreQueryFilters so an archived grade/specialization/subject still labels the page.
        var gradeName = await db.Grades
            .IgnoreQueryFilters()
            .Where(g => g.Id == session.GradeId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var specialization = await db.Specializations
            .IgnoreQueryFilters()
            .Where(sp => sp.Id == session.SpecializationId)
            .Select(sp => new { sp.Name, sp.SubjectId })
            .FirstOrDefaultAsync(cancellationToken);

        var subjectId = specialization?.SubjectId ?? Guid.Empty;
        string? subjectName = specialization is null
            ? null
            : await db.Subjects
                .IgnoreQueryFilters()
                .Where(su => su.Id == specialization.SubjectId)
                .Select(su => su.Name)
                .FirstOrDefaultAsync(cancellationToken);

        string? thumbnailUrl = null;
        if (!string.IsNullOrWhiteSpace(session.ThumbnailObjectKey))
        {
            var signed = await fileStorage.GetSignedReadUrlAsync(
                session.ThumbnailObjectKey, cancellationToken: cancellationToken);
            thumbnailUrl = signed.Url;
        }

        // Progress (§E.1) + expiry (§E.2).
        var videoCount = session.Videos.Count;
        var videosWatched = enrollment.VideoAccesses.Count(a => a.AccessRemaining < a.AccessAllowed);
        var isExpired = enrollment.ExpiresAtUtc is { } expiresAt && expiresAt <= now;
        var progressPercent = videoCount == 0
            ? 0
            : (int)Math.Round(100.0 * videosWatched / videoCount, MidpointRounding.AwayFromZero);

        // Gate banner (§E.4) — a UserQuiz exists for the enrollment only when the session is quiz-gated; this is the
        // exact predicate StartVideoPlaybackHandler/GetHlsKeyHandler use for quiz_required.
        var quiz = await db.UserQuizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.EnrollmentId == enrollment.Id, cancellationToken);
        var hasGatingQuiz = quiz is not null;
        var quizPassed = quiz?.Passed == true;
        var minPassPercent = quiz?.MinPassPercent ?? 0;
        var gateState = isExpired
            ? MySessionGateState.Expired
            : hasGatingQuiz && !quizPassed
                ? MySessionGateState.QuizRequired
                : MySessionGateState.Open;

        // Video playlist (ordered) with the caller's per-video budget + lock state mirroring the 5C gate (§E.3).
        var videos = session.Videos
            .OrderBy(v => v.Order)
            .Select(v =>
            {
                var access = enrollment.VideoAccesses.FirstOrDefault(a => a.VideoId == v.Id);
                var accessAllowed = access?.AccessAllowed ?? 0;
                var accessRemaining = access?.AccessRemaining ?? 0;
                var lockState = DeriveLockState(
                    isExpired, hasGatingQuiz, quizPassed, v.ProcessingStatus, accessRemaining);
                return v.ToMyVideoDto(accessAllowed, accessRemaining, lockState);
            })
            .ToList();

        var materials = session.Materials
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => m.ToMyMaterialDto())
            .ToList();

        // Assignment status (null only when the session has no assignment snapshot).
        var assignment = await db.UserAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EnrollmentId == enrollment.Id, cancellationToken);

        return session.ToMyDetailDto(
            gradeName,
            subjectId,
            subjectName,
            specialization?.Name,
            thumbnailUrl,
            enrollment.Id,
            enrollment.EnrolledAtUtc,
            enrollment.ExpiresAtUtc,
            isExpired,
            videoCount,
            videosWatched,
            progressPercent,
            gateState,
            hasGatingQuiz,
            quizPassed,
            minPassPercent,
            videos,
            materials,
            assignment?.ToMyStatusDto(),
            quiz?.ToMyStatusDto());
    }

    /// <summary>First matching rule wins, in the 5C gate's authorization order (§E.3): a session-expired or
    /// quiz-gated session locks <b>all</b> videos before the per-video not-ready/exhausted checks apply.</summary>
    private static MyVideoLockState DeriveLockState(
        bool isExpired,
        bool hasGatingQuiz,
        bool quizPassed,
        VideoProcessingStatus processingStatus,
        int accessRemaining)
    {
        if (isExpired)
            return MyVideoLockState.Expired;
        if (hasGatingQuiz && !quizPassed)
            return MyVideoLockState.QuizLocked;
        if (processingStatus != VideoProcessingStatus.Ready)
            return MyVideoLockState.NotReady;
        if (accessRemaining == 0)
            return MyVideoLockState.Exhausted;
        return MyVideoLockState.Playable;
    }
}
