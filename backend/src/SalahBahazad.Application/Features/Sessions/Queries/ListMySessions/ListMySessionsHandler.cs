using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListMySessions;

/// <summary>
/// Projects the caller's enrolled sessions (contract §A/§E). The enrolled set is the caller's own non-refunded
/// <c>Enrollment</c> rows (tenant + soft-delete scoping is automatic via the global query filter), newest-enrolled
/// first. Progress reuses the attendance projector's predicate exactly (<c>AccessRemaining &lt; AccessAllowed</c>,
/// §E.1); <c>isExpired</c> + completion <c>state</c> are derived (§E.2 — <c>Status</c> is never flipped to Expired).
/// Name resolution mirrors <c>ListCatalogueHandler</c> (<c>IgnoreQueryFilters</c> on grade/subject/spec <b>names</b>
/// so an archived taxonomy row still labels the card).
/// </summary>
internal sealed class ListMySessionsHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock)
    : IRequestHandler<ListMySessionsQuery, IReadOnlyList<MySessionDto>>
{
    public async ValueTask<IReadOnlyList<MySessionDto>> Handle(
        ListMySessionsQuery query, CancellationToken cancellationToken)
    {
        var studentId = currentUser.UserId;
        var now = clock.GetUtcNow();

        // The caller's own non-refunded enrollments, newest first. videosWatched is the count of per-video access
        // counters with a spent view — the exact predicate AttendanceProjector.WatchedByEnrollmentAsync uses (§E.1).
        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status != EnrollmentStatus.Refunded)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .Select(e => new EnrollmentRow(
                e.Id,
                e.SessionId,
                e.EnrolledAtUtc,
                e.ExpiresAtUtc,
                e.VideoAccesses.Count(a => a.AccessRemaining < a.AccessAllowed)))
            .ToListAsync(cancellationToken);

        if (enrollments.Count == 0)
            return [];

        var sessionIds = enrollments.Select(e => e.SessionId).Distinct().ToList();

        var sessions = await db.Sessions
            .AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var sessionById = sessions.ToDictionary(s => s.Id);

        var videoCounts = await db.SessionVideos
            .Where(v => sessionIds.Contains(v.SessionId))
            .GroupBy(v => v.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);

        // Resolve display names. IgnoreQueryFilters so an archived (soft-deleted) grade/specialization still
        // shows its name rather than the row losing its label (FR-PLAT-ROLE-004).
        var gradeIds = sessions.Select(s => s.GradeId).Distinct().ToList();
        var gradeNames = await db.Grades
            .IgnoreQueryFilters()
            .Where(g => gradeIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var specIds = sessions.Select(s => s.SpecializationId).Distinct().ToList();
        var specs = await db.Specializations
            .IgnoreQueryFilters()
            .Where(sp => specIds.Contains(sp.Id))
            .Select(sp => new { sp.Id, sp.Name, sp.SubjectId })
            .ToListAsync(cancellationToken);
        var specById = specs.ToDictionary(x => x.Id);

        var subjectIds = specs.Select(x => x.SubjectId).Distinct().ToList();
        var subjectNames = await db.Subjects
            .IgnoreQueryFilters()
            .Where(su => subjectIds.Contains(su.Id))
            .ToDictionaryAsync(su => su.Id, su => su.Name, cancellationToken);

        var dtos = new List<MySessionDto>(enrollments.Count);
        foreach (var e in enrollments)
        {
            // A soft-deleted session is excluded by the global filter; skip the row rather than show a blank card.
            if (!sessionById.TryGetValue(e.SessionId, out var s))
                continue;

            var videoCount = videoCounts.GetValueOrDefault(s.Id);
            var isExpired = e.ExpiresAtUtc is { } expiresAt && expiresAt <= now;
            var progressPercent = videoCount == 0
                ? 0
                : (int)Math.Round(100.0 * e.VideosWatched / videoCount, MidpointRounding.AwayFromZero);
            var state = DeriveCompletion(videoCount, e.VideosWatched);

            if (!MatchesFilter(query.State, state, isExpired, e.ExpiresAtUtc, now))
                continue;

            // Sign the thumbnail only for rows that survive the filter (§A.2; null key → null url).
            string? thumbnailUrl = null;
            if (!string.IsNullOrWhiteSpace(s.ThumbnailObjectKey))
            {
                var signed = await fileStorage.GetSignedReadUrlAsync(
                    s.ThumbnailObjectKey, cancellationToken: cancellationToken);
                thumbnailUrl = signed.Url;
            }

            specById.TryGetValue(s.SpecializationId, out var spec);
            var subjectName = spec is null ? null : subjectNames.GetValueOrDefault(spec.SubjectId);

            dtos.Add(s.ToMyDto(
                e.Id,
                gradeNames.GetValueOrDefault(s.GradeId),
                subjectName,
                spec?.Name,
                thumbnailUrl,
                videoCount,
                e.VideosWatched,
                progressPercent,
                e.EnrolledAtUtc,
                e.ExpiresAtUtc,
                isExpired,
                state));
        }

        return dtos;
    }

    /// <summary>Completion only (independent of expiry, §E.2): <c>Completed</c> iff every video has a spent view;
    /// <c>NotStarted</c> iff none does; else <c>InProgress</c>. A session with no videos is <c>NotStarted</c>.</summary>
    private static MySessionCompletionState DeriveCompletion(int videoCount, int videosWatched)
        => videoCount > 0 && videosWatched == videoCount ? MySessionCompletionState.Completed
            : videosWatched == 0 ? MySessionCompletionState.NotStarted
            : MySessionCompletionState.InProgress;

    /// <summary>Applies the optional <c>?state=</c> chip (§A.1/§E.2). The completion chips match only non-expired
    /// rows; <c>ExpiringSoon</c> = non-expired with an expiry within 14 days; <c>Expired</c> = past expiry.</summary>
    private static bool MatchesFilter(
        MySessionState? filter,
        MySessionCompletionState completion,
        bool isExpired,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset now)
        => filter switch
        {
            null => true,
            MySessionState.Expired => isExpired,
            MySessionState.ExpiringSoon =>
                !isExpired && expiresAtUtc is { } e && e <= now.AddDays(14),
            MySessionState.NotStarted => !isExpired && completion == MySessionCompletionState.NotStarted,
            MySessionState.InProgress => !isExpired && completion == MySessionCompletionState.InProgress,
            MySessionState.Completed => !isExpired && completion == MySessionCompletionState.Completed,
            _ => true,
        };

    private sealed record EnrollmentRow(
        Guid Id, Guid SessionId, DateTimeOffset EnrolledAtUtc, DateTimeOffset? ExpiresAtUtc, int VideosWatched);
}
