using System.Globalization;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMyPlan;

/// <summary>
/// Composes the caller's weekly study plan (contract §E, Path A — current-frontier). All inputs are the caller's
/// own rows, tenant + soft-delete scoped automatically by the global query filter. The derivations reuse the S3
/// primitives verbatim (§E.1): videos-watched = the per-video access counters the 5C gate decrements
/// (<c>AccessRemaining &lt; AccessAllowed</c>, the same predicate as <see cref="Attendance.AttendanceProjector"/>),
/// <c>isExpired</c>/completion derived (the writer never flips <c>EnrollmentStatus</c> to Expired), <c>quizPassed</c>
/// = <see cref="Domain.Entities.UserQuiz.Passed"/>, assignment status from <see cref="Domain.Entities.UserAssignment"/>.
/// Every read is batched per concern (one round-trip each, no N+1, NFR-PERF-005); the whole composition is cached
/// in HybridCache for the ISO week (§C) and recomputed only on a miss or after an invalidation (§D).
/// </summary>
internal sealed class GetMyPlanHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    IFileStorage fileStorage,
    TimeProvider clock,
    HybridCache cache)
    : IRequestHandler<GetMyPlanQuery, MyPlanDto>
{
    private const int MaxSteps = 7;                      // §E.3 structural anti-overwhelm cap
    private const int MaxSecondaryNudges = 2;            // §E.3.5 cross-session expiry nudges
    private const int MaxExpiredAssignmentSteps = 3;     // §E.4 expired-only assignment steps
    private const int RecentLimit = 5;                   // §E.5 recently-enrolled rail
    private const int ExpiringSoonDays = 14;             // §B ExpiringSoon window
    private static readonly TimeSpan MinTtl = TimeSpan.FromMinutes(5);   // §C TTL floor

    public async ValueTask<MyPlanDto> Handle(GetMyPlanQuery query, CancellationToken cancellationToken)
    {
        var studentId = currentUser.UserId;
        // Capture the tenant in the request scope: the HybridCache factory below runs without the request's
        // HttpContext, so the EF global tenant filter (which reads it) is unusable inside it. The cached reads
        // therefore scope the tenant explicitly (IgnoreQueryFilters + an explicit TenantId/!IsDeleted predicate),
        // the same pattern Phase 5A used where the ambient filter doesn't apply.
        var tenantId = currentUser.TenantId;
        var now = clock.GetUtcNow();

        // ── ISO week frame + the cache window (§C). The week rolls naturally as the TTL lapses at next Monday. ──
        var isoYear = ISOWeek.GetYear(now.UtcDateTime);
        var isoWeekNumber = ISOWeek.GetWeekOfYear(now.UtcDateTime);
        var isoWeek = $"{isoYear:D4}-W{isoWeekNumber:D2}";
        var weekStart = new DateTimeOffset(
            ISOWeek.ToDateTime(isoYear, isoWeekNumber, DayOfWeek.Monday), TimeSpan.Zero);
        var nextWeekStart = weekStart.AddDays(7);
        var weekEnd = nextWeekStart.AddSeconds(-1);      // Sunday 23:59:59Z

        var ttl = nextWeekStart - now;
        if (ttl < MinTtl)
            ttl = MinTtl;

        var options = new HybridCacheEntryOptions
        {
            Expiration = ttl,
            // ≤ 60 s so a single node never serves a step stale past another node's invalidation (§C/§D).
            LocalCacheExpiration = TimeSpan.FromSeconds(60),
        };

        // Tenant in the key matches the explicit scope inside the factory (the global filter is unusable there).
        var key = $"plan:{tenantId}:{studentId}:{isoWeek}";
        var snapshot = await cache.GetOrCreateAsync(
            key,
            async ct => await ComputeSnapshotAsync(tenantId, studentId, now, isoWeek, weekStart, weekEnd, ct),
            options,
            tags: [$"plan:{studentId}"],
            cancellationToken: cancellationToken);

        // The focus thumbnail URL is signed fresh per request — never cached — so it stays short-lived even though
        // the snapshot is cached for the whole ISO week (§C; the snapshot carries only the R2 object key).
        string? thumbnailUrl = null;
        if (snapshot.Focus?.ThumbnailObjectKey is { } objectKey && !string.IsNullOrWhiteSpace(objectKey))
        {
            var signed = await fileStorage.GetSignedReadUrlAsync(objectKey, cancellationToken: cancellationToken);
            thumbnailUrl = signed.Url;
        }

        return snapshot.ToDto(thumbnailUrl);
    }

    private async Task<PlanSnapshot> ComputeSnapshotAsync(
        Guid tenantId,
        Guid studentId,
        DateTimeOffset now,
        string isoWeek,
        DateTimeOffset weekStart,
        DateTimeOffset weekEnd,
        CancellationToken cancellationToken)
    {
        // 1. The caller's non-refunded enrollments, newest first. videosWatched is the count of spent per-video
        //    counters — the exact predicate AttendanceProjector.WatchedByEnrollmentAsync uses (§E.1). Tenant +
        //    soft-delete are scoped explicitly because the global filter is unusable inside the cache factory.
        var enrollments = await db.Enrollments
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted
                        && e.StudentId == studentId && e.Status != EnrollmentStatus.Refunded)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .Select(e => new EnrollmentRow(
                e.Id,
                e.SessionId,
                e.EnrolledAtUtc,
                e.ExpiresAtUtc,
                e.VideoAccesses.Count(a => a.AccessRemaining < a.AccessAllowed)))
            .ToListAsync(cancellationToken);

        if (enrollments.Count == 0)
            return EmptyPlan(isoWeek, weekStart, weekEnd, now);   // §E.4 onboarding (one Redeem step)

        var sessionIds = enrollments.Select(e => e.SessionId).Distinct().ToList();
        var enrollmentIds = enrollments.Select(e => e.Id).ToList();

        // Exclude soft-deleted sessions explicitly (the global filter is unusable here); a skipped session simply
        // drops its enrollment row below. The session ids already belong to the tenant via the enrollment scope,
        // and the explicit TenantId keeps it defence-in-depth.
        var sessions = await db.Sessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && sessionIds.Contains(s.Id))
            .Select(s => new SessionRow(
                s.Id, s.Title, s.SpecializationId, s.ThumbnailObjectKey, s.PrerequisiteSessionId))
            .ToListAsync(cancellationToken);
        var sessionById = sessions.ToDictionary(s => s.Id);

        var videoCounts = await db.SessionVideos
            .Where(v => sessionIds.Contains(v.SessionId))
            .GroupBy(v => v.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);

        // A UserQuiz exists for an enrollment only when the session is quiz-gated; Passed is the ≥-pass state the
        // 5C gate reads (the same predicate GetMySessionHandler uses, §E.1). Constrained by the tenant-scoped
        // enrollmentIds (so IgnoreQueryFilters here is tenant-safe — the rows can only be this tenant's).
        var quizByEnrollment = (await db.UserQuizzes
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(q => enrollmentIds.Contains(q.EnrollmentId))
                .Select(q => new { q.EnrollmentId, q.Id, q.Passed })
                .ToListAsync(cancellationToken))
            .ToDictionary(q => q.EnrollmentId);

        // Assignment status + the answered count (answered = SelectedOptionId set, i.e. IsAnswered) batched per
        // enrollment — one subquery count per row, bounded by the enrollment set (no N+1). Tenant-safe via the
        // tenant-scoped enrollmentIds (IgnoreQueryFilters because the global tenant filter is unusable here).
        var assignmentByEnrollment = (await db.UserAssignments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .Select(a => new AssignmentRow(
                    a.EnrollmentId,
                    a.Id,
                    a.Status,
                    a.QuestionCount,
                    a.Questions.Count(q => q.SelectedOptionId != null),
                    a.ScoreMarks,
                    a.MaxMarks))
                .ToListAsync(cancellationToken))
            .ToDictionary(a => a.EnrollmentId);

        // Specialization names for the per-row accent chip — IgnoreQueryFilters so an archived spec still labels
        // the card (mirrors ListMySessionsHandler).
        var specIds = sessions.Select(s => s.SpecializationId).Distinct().ToList();
        var specNames = await db.Specializations
            .IgnoreQueryFilters()
            .Where(sp => specIds.Contains(sp.Id))
            .ToDictionaryAsync(sp => sp.Id, sp => sp.Name, cancellationToken);

        // 2. Per-enrollment working state (§E.1 derivations).
        var states = new List<EnrolledState>(enrollments.Count);
        foreach (var e in enrollments)
        {
            if (!sessionById.TryGetValue(e.SessionId, out var s))
                continue;   // soft-deleted session — skip rather than show a blank step

            var videoCount = videoCounts.GetValueOrDefault(s.Id);
            var isExpired = e.ExpiresAtUtc is { } expiresAt && expiresAt <= now;
            var progress = videoCount == 0
                ? 0
                : (int)Math.Round(100.0 * e.VideosWatched / videoCount, MidpointRounding.AwayFromZero);
            quizByEnrollment.TryGetValue(e.Id, out var quiz);
            assignmentByEnrollment.TryGetValue(e.Id, out var assignment);

            states.Add(new EnrolledState(
                EnrollmentId: e.Id,
                SessionId: s.Id,
                Title: s.Title,
                SpecializationName: specNames.GetValueOrDefault(s.SpecializationId),
                ThumbnailObjectKey: s.ThumbnailObjectKey,
                PrerequisiteSessionId: s.PrerequisiteSessionId,
                EnrolledAtUtc: e.EnrolledAtUtc,
                ExpiresAtUtc: e.ExpiresAtUtc,
                IsExpired: isExpired,
                VideoCount: videoCount,
                VideosWatched: e.VideosWatched,
                ProgressPercent: progress,
                Completion: DeriveCompletion(videoCount, e.VideosWatched),
                HasGatingQuiz: quiz is not null,
                QuizPassed: quiz?.Passed == true,
                UserQuizId: quiz?.Id,
                HasAssignment: assignment is not null,
                AssignmentCompleted: assignment?.Status == AssignmentStatus.Completed,
                UserAssignmentId: assignment?.Id,
                AssignmentAnswered: assignment?.Answered ?? 0,
                AssignmentQuestionCount: assignment?.QuestionCount ?? 0,
                AssignmentScoreMarks: assignment?.ScoreMarks,
                AssignmentMaxMarks: assignment?.MaxMarks ?? 0));
        }

        // One non-refunded enrollment per session (ENR-006); newest wins for the depth walk's ancestor lookup.
        var bySession = states
            .GroupBy(s => s.SessionId)
            .ToDictionary(g => g.Key, g => g.First());

        // 3. Pick the focus (§E.2): active & incomplete, soonest expiry (nulls last), newest enrolled, then the
        //    prerequisite-depth tie-break (earliest in its enrolled chain finishes first).
        var focus = states
            .Where(s => s.Active && s.Incomplete)
            .OrderBy(s => s.ExpiresAtUtc.HasValue ? 0 : 1)
            .ThenBy(s => s.ExpiresAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(s => s.EnrolledAtUtc)
            .ThenBy(s => PrerequisiteDepth(s, bySession))
            .FirstOrDefault();

        var steps = new List<MyPlanStepDto>();
        if (focus is not null)
        {
            BuildFocusSteps(focus, now, steps);

            // 4. Secondary expiry nudges for OTHER active, incomplete, expiring-soon enrollments (§E.3.5, ≤ 2).
            var nudges = states
                .Where(s => s.SessionId != focus.SessionId
                            && s.Active && s.Incomplete
                            && s.ExpiresAtUtc is { } e && e <= now.AddDays(ExpiringSoonDays))
                .OrderBy(s => s.ExpiresAtUtc!.Value)
                .Take(MaxSecondaryNudges);
            foreach (var s in nudges)
                steps.Add(BuildNudgeStep(s, now));
        }
        else
        {
            BuildResidualSteps(states, steps);   // §E.4 expired-only / all-done
        }

        // 5. Cap at 7 — drop later/lower-priority items first.
        if (steps.Count > MaxSteps)
            steps = steps.Take(MaxSteps).ToList();

        var kpis = BuildKpis(states);

        var recent = enrollments
            .Where(e => sessionById.ContainsKey(e.SessionId))
            .Take(RecentLimit)
            .Select(e =>
            {
                var s = sessionById[e.SessionId];
                return new MyPlanRecentDto(
                    s.Id, s.Title, specNames.GetValueOrDefault(s.SpecializationId), e.EnrolledAtUtc);
            })
            .ToList();

        var focusSnapshot = focus is null
            ? null
            : new PlanFocusSnapshot(
                focus.SessionId,
                focus.Title,
                focus.SpecializationName,
                focus.ThumbnailObjectKey,
                focus.ProgressPercent,
                focus.ExpiresAtUtc,
                focus.IsExpired,
                ExpiresInDays(focus.ExpiresAtUtc, now),
                DueStateFor(focus, completed: false, now));   // focus is active → None or ExpiringSoon, never Expired

        return new PlanSnapshot(
            isoWeek,
            weekStart,
            weekEnd,
            now,
            TotalSteps: steps.Count,
            CompletedSteps: steps.Count(s => s.Status == MyPlanStepStatus.Completed),
            OverdueSteps: steps.Count(s => s.DueState == MyPlanDueState.Expired
                                           && s.Status != MyPlanStepStatus.Completed),
            kpis,
            focusSnapshot,
            steps,
            recent);
    }

    /// <summary>
    /// The focus session's gate-ordered steps (§E.3): quiz → videos → assignment. Steps are added by
    /// <b>existence</b> (a gating quiz / videos / an assignment) and carry their derived <see cref="MyPlanStepStatus"/>,
    /// so a completed sub-task is kept in the list for the week bar + the "Completed" sub-list (§A.1 invariant
    /// <c>completedSteps == count(status == Completed)</c>). The roll-forward Redeem (§E.3.4) never applies here —
    /// the focus is incomplete by selection — so it lives only in the all-done plan (§E.4).
    /// </summary>
    private static void BuildFocusSteps(EnrolledState f, DateTimeOffset now, List<MyPlanStepDto> steps)
    {
        var route = $"/sessions/{f.SessionId}";

        if (f.HasGatingQuiz)
        {
            var passed = f.QuizPassed;
            steps.Add(new MyPlanStepDto(
                Key: $"quiz:{f.UserQuizId}",
                Kind: MyPlanStepKind.Quiz,
                Title: "Pass the gating quiz",
                Subtitle: $"Unlocks {f.VideoCount} videos",
                SessionId: f.SessionId,
                SessionTitle: f.Title,
                SpecializationName: f.SpecializationName,
                Status: passed ? MyPlanStepStatus.Completed : MyPlanStepStatus.Pending,
                Blocked: false,
                BlockedReason: null,
                DueState: DueStateFor(f, passed, now),
                ExpiresAtUtc: f.ExpiresAtUtc,
                Progress: null,
                Action: new MyPlanActionDto(MyPlanActionType.Navigate, route, passed ? "Continue" : "Start")));
        }

        if (f.VideoCount > 0)
        {
            var done = f.VideosWatched >= f.VideoCount;
            var blocked = f.HasGatingQuiz && !f.QuizPassed;   // videos locked until the gating quiz passes
            steps.Add(new MyPlanStepDto(
                Key: $"videos:{f.SessionId}",
                Kind: MyPlanStepKind.Videos,
                Title: "Watch your lessons",
                Subtitle: $"{f.VideosWatched} of {f.VideoCount} watched",
                SessionId: f.SessionId,
                SessionTitle: f.Title,
                SpecializationName: f.SpecializationName,
                Status: done ? MyPlanStepStatus.Completed : MyPlanStepStatus.Pending,
                Blocked: blocked,
                BlockedReason: blocked ? "Pass the quiz to unlock the videos" : null,
                DueState: DueStateFor(f, done, now),
                ExpiresAtUtc: f.ExpiresAtUtc,
                Progress: new MyPlanProgressDto(f.VideosWatched, f.VideoCount),
                Action: new MyPlanActionDto(
                    MyPlanActionType.Navigate, route, f.VideosWatched == 0 ? "Watch" : "Continue")));
        }

        if (f.HasAssignment)
        {
            // Reachable even when the session is expired (FR-STU-SES-001) — never blocked by expiry.
            var done = f.AssignmentCompleted;
            steps.Add(new MyPlanStepDto(
                Key: $"assignment:{f.UserAssignmentId}",
                Kind: MyPlanStepKind.Assignment,
                Title: "Finish your assignment",
                Subtitle: done
                    ? $"Score {f.AssignmentScoreMarks}/{f.AssignmentMaxMarks}"
                    : $"{f.AssignmentAnswered} of {f.AssignmentQuestionCount} answered",
                SessionId: f.SessionId,
                SessionTitle: f.Title,
                SpecializationName: f.SpecializationName,
                Status: done ? MyPlanStepStatus.Completed : MyPlanStepStatus.Pending,
                Blocked: false,
                BlockedReason: null,
                DueState: DueStateFor(f, done, now),
                ExpiresAtUtc: f.ExpiresAtUtc,
                Progress: new MyPlanProgressDto(f.AssignmentAnswered, f.AssignmentQuestionCount),
                Action: new MyPlanActionDto(MyPlanActionType.Navigate, route, "Open")));
        }
    }

    /// <summary>A compact cross-session nudge for an expiring-soon enrollment (§E.3.5): the only time pressure in
    /// the plan. Videos when lessons remain, else the still-open assignment.</summary>
    private static MyPlanStepDto BuildNudgeStep(EnrolledState s, DateTimeOffset now)
    {
        var days = ExpiresInDays(s.ExpiresAtUtc, now) ?? 0;
        var videosDone = s.VideoCount > 0 && s.VideosWatched >= s.VideoCount;
        var useAssignment = videosDone && s.HasAssignment && !s.AssignmentCompleted;
        return new MyPlanStepDto(
            Key: useAssignment ? $"assignment:{s.UserAssignmentId}" : $"videos:{s.SessionId}",
            Kind: useAssignment ? MyPlanStepKind.Assignment : MyPlanStepKind.Videos,
            Title: $"{s.Title} expires soon",
            Subtitle: $"Expires in {days} days — finish your lessons",
            SessionId: s.SessionId,
            SessionTitle: s.Title,
            SpecializationName: s.SpecializationName,
            Status: MyPlanStepStatus.Pending,
            Blocked: false,
            BlockedReason: null,
            DueState: MyPlanDueState.ExpiringSoon,
            ExpiresAtUtc: s.ExpiresAtUtc,
            Progress: useAssignment
                ? new MyPlanProgressDto(s.AssignmentAnswered, s.AssignmentQuestionCount)
                : new MyPlanProgressDto(s.VideosWatched, s.VideoCount),
            Action: new MyPlanActionDto(MyPlanActionType.Navigate, $"/sessions/{s.SessionId}", "Continue"));
    }

    /// <summary>
    /// The plan when there is no active, incomplete enrollment (§E.4): surface each expired enrollment whose
    /// assignment is still open (reachable past expiry, FR-STU-SES-001) as an <c>Expired</c> Assignment step
    /// (≤ 3), then a trailing <c>Redeem</c> — "Renew access" when any such step exists, else the roll-forward
    /// "Ready for your next session". A specific successor is never fabricated; enrollment is code-only.
    /// </summary>
    private static void BuildResidualSteps(IReadOnlyList<EnrolledState> states, List<MyPlanStepDto> steps)
    {
        var expiredOpenAssignments = states
            .Where(s => s.IsExpired && s.HasAssignment && !s.AssignmentCompleted)
            .OrderBy(s => s.ExpiresAtUtc ?? DateTimeOffset.MaxValue)
            .Take(MaxExpiredAssignmentSteps)
            .ToList();

        foreach (var s in expiredOpenAssignments)
            steps.Add(new MyPlanStepDto(
                Key: $"assignment:{s.UserAssignmentId}",
                Kind: MyPlanStepKind.Assignment,
                Title: "Finish your assignment",
                Subtitle: $"{s.AssignmentAnswered} of {s.AssignmentQuestionCount} answered",
                SessionId: s.SessionId,
                SessionTitle: s.Title,
                SpecializationName: s.SpecializationName,
                Status: MyPlanStepStatus.Pending,
                Blocked: false,
                BlockedReason: null,
                DueState: MyPlanDueState.Expired,
                ExpiresAtUtc: s.ExpiresAtUtc,
                Progress: new MyPlanProgressDto(s.AssignmentAnswered, s.AssignmentQuestionCount),
                Action: new MyPlanActionDto(MyPlanActionType.Navigate, $"/sessions/{s.SessionId}", "Open")));

        steps.Add(expiredOpenAssignments.Count > 0
            ? RedeemStep("redeem:renew", "Renew access", "Get a new code from your teacher")
            : RedeemStep("redeem:next", "Ready for your next session", "Get a code from your teacher to unlock it"));
    }

    private static MyPlanKpisDto BuildKpis(IReadOnlyList<EnrolledState> states)
    {
        // Videos/active/progress sum over the active (non-expired) set; completed counts the whole set (§E.5).
        var active = states.Where(s => s.Active).ToList();
        var videosWatched = active.Sum(s => s.VideosWatched);
        var videosTotal = active.Sum(s => s.VideoCount);
        return new MyPlanKpisDto(
            ActiveSessions: active.Count(s => s.Completion != MySessionCompletionState.Completed),
            VideosWatched: videosWatched,
            VideosTotal: videosTotal,
            OverallProgressPercent: videosTotal == 0
                ? 0
                : (int)Math.Round(100.0 * videosWatched / videosTotal, MidpointRounding.AwayFromZero),
            CompletedSessions: states.Count(s => s.Completion == MySessionCompletionState.Completed));
    }

    private static PlanSnapshot EmptyPlan(
        string isoWeek, DateTimeOffset weekStart, DateTimeOffset weekEnd, DateTimeOffset now)
        => new(
            isoWeek,
            weekStart,
            weekEnd,
            now,
            TotalSteps: 1,
            CompletedSteps: 0,
            OverdueSteps: 0,
            Kpis: new MyPlanKpisDto(0, 0, 0, 0, 0),
            Focus: null,
            Steps: [RedeemStep("redeem:onboarding", "Redeem a code", "Unlock your first session")],
            RecentlyEnrolled: []);

    private static MyPlanStepDto RedeemStep(string key, string title, string subtitle)
        => new(
            Key: key,
            Kind: MyPlanStepKind.Redeem,
            Title: title,
            Subtitle: subtitle,
            SessionId: Guid.Empty,
            SessionTitle: string.Empty,
            SpecializationName: null,
            Status: MyPlanStepStatus.Pending,
            Blocked: false,
            BlockedReason: null,
            DueState: MyPlanDueState.None,
            ExpiresAtUtc: null,
            Progress: null,
            Action: new MyPlanActionDto(MyPlanActionType.Redeem, null, "Redeem"));

    /// <summary>Completion only (independent of expiry, §E.1): <c>Completed</c> iff every video has a spent view;
    /// <c>NotStarted</c> iff none does; else <c>InProgress</c>. No videos ⇒ <c>NotStarted</c>.</summary>
    private static MySessionCompletionState DeriveCompletion(int videoCount, int videosWatched)
        => videoCount > 0 && videosWatched == videoCount ? MySessionCompletionState.Completed
            : videosWatched == 0 ? MySessionCompletionState.NotStarted
            : MySessionCompletionState.InProgress;

    /// <summary>A step's due state from its session expiry only (§E.3): <c>Expired</c> when past expiry and the
    /// step is incomplete; else <c>ExpiringSoon</c> when active and within the window; else <c>None</c>.</summary>
    private static MyPlanDueState DueStateFor(EnrolledState s, bool completed, DateTimeOffset now)
        => s.IsExpired && !completed ? MyPlanDueState.Expired
            : !s.IsExpired && s.ExpiresAtUtc is { } e && e <= now.AddDays(ExpiringSoonDays)
                ? MyPlanDueState.ExpiringSoon
                : MyPlanDueState.None;

    /// <summary><c>ceil((expiresAtUtc - now)/1d)</c>, floored at 0; null when the session has no expiry (§A.1).</summary>
    private static int? ExpiresInDays(DateTimeOffset? expiresAtUtc, DateTimeOffset now)
        => expiresAtUtc is { } e ? Math.Max(0, (int)Math.Ceiling((e - now).TotalDays)) : null;

    /// <summary>Number of the session's prerequisite ancestors that are <b>also</b> in the caller's enrolled set
    /// (§E.2 tie-break) — computed over the already-loaded rows only, no extra query; cycle-guarded.</summary>
    private static int PrerequisiteDepth(EnrolledState s, IReadOnlyDictionary<Guid, EnrolledState> bySession)
    {
        var depth = 0;
        var seen = new HashSet<Guid> { s.SessionId };
        var cursor = s.PrerequisiteSessionId;
        while (cursor is { } prereq && bySession.TryGetValue(prereq, out var ancestor) && seen.Add(prereq))
        {
            depth++;
            cursor = ancestor.PrerequisiteSessionId;
        }

        return depth;
    }

    private sealed record EnrollmentRow(
        Guid Id, Guid SessionId, DateTimeOffset EnrolledAtUtc, DateTimeOffset? ExpiresAtUtc, int VideosWatched);

    private sealed record SessionRow(
        Guid Id, string Title, Guid SpecializationId, string? ThumbnailObjectKey, Guid? PrerequisiteSessionId);

    private sealed record AssignmentRow(
        Guid EnrollmentId, Guid Id, AssignmentStatus Status, int QuestionCount, int Answered,
        int? ScoreMarks, int MaxMarks);

    /// <summary>The caller's per-enrollment working state (§E.1). <see cref="Active"/>/<see cref="Incomplete"/>
    /// drive focus selection (§E.2); the steps read the derived fields directly.</summary>
    private sealed record EnrolledState(
        Guid EnrollmentId,
        Guid SessionId,
        string Title,
        string? SpecializationName,
        string? ThumbnailObjectKey,
        Guid? PrerequisiteSessionId,
        DateTimeOffset EnrolledAtUtc,
        DateTimeOffset? ExpiresAtUtc,
        bool IsExpired,
        int VideoCount,
        int VideosWatched,
        int ProgressPercent,
        MySessionCompletionState Completion,
        bool HasGatingQuiz,
        bool QuizPassed,
        Guid? UserQuizId,
        bool HasAssignment,
        bool AssignmentCompleted,
        Guid? UserAssignmentId,
        int AssignmentAnswered,
        int AssignmentQuestionCount,
        int? AssignmentScoreMarks,
        int AssignmentMaxMarks)
    {
        public bool Active => !IsExpired;

        public bool Incomplete =>
            Completion != MySessionCompletionState.Completed
            || (HasAssignment && !AssignmentCompleted)
            || (HasGatingQuiz && !QuizPassed);
    }
}
