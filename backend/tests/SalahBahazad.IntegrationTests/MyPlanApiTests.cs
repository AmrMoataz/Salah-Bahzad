using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The student-portal Home weekly study plan (GET /api/me/plan, contract §A/§B/§E, FR-STU-SES-001 et al.):
/// the focus matrix (§E.2/§E.3), the prerequisite-depth tie-break (§E.2), the secondary-nudge cap (§E.3.5), the
/// empty/expired-only/all-done shapes (§E.4), the KPIs + recently-enrolled rail (§E.5), the cache-invalidation
/// path proven through the real engines (§C/§D — watch a video / pass the quiz / complete the assignment / enrol /
/// refund → the step moves on the next read with no TTL wait), cross-tenant isolation (NFR-SEC-010),
/// "read, not audited" (§F), and 401/403/200 role gating (§A.2).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyPlanApiTests(SalahBahazadApiFactory factory)
{
    private async Task<(Guid Tenant, Guid GradeId, Guid SubjectId, Guid SpecId, StudentEntity Student)> SetupAsync()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, subjectId, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        return (tenant, gradeId, subjectId, specId, student);
    }

    private async Task<MyPlanResponse> GetPlanAsync(HttpClient client)
        => (await client.GetFromJsonAsync<MyPlanResponse>("/api/me/plan", TestJson.Options))!;

    private async Task<EnrollmentResult> EnrollAsync(HttpClient student, HttpClient teacher, Guid sessionId)
        => await student.RedeemAsync(await teacher.GenerateOneSerialAsync(sessionId));

    /// <summary>Spends one view on the first <paramref name="videos"/> distinct counters (each then counts as
    /// "watched": AccessRemaining &lt; AccessAllowed — the 5C-gate source).</summary>
    private Task SpendViewsAsync(Guid enrollmentId, int videos)
        => factory.QueryDbAsync<int>(async db =>
        {
            var enrollment = await db.Enrollments
                .IgnoreQueryFilters().Include(e => e.VideoAccesses).FirstAsync(e => e.Id == enrollmentId);
            foreach (var access in enrollment.VideoAccesses.OrderBy(a => a.VideoId).Take(videos))
                access.Decrement();
            await db.SaveChangesAsync();
            return videos;
        });

    /// <summary>Spends one view on every counter so the enrollment's session is completion-Completed.</summary>
    private Task SpendAllViewsAsync(Guid enrollmentId)
        => factory.QueryDbAsync<int>(async db =>
        {
            var enrollment = await db.Enrollments
                .IgnoreQueryFilters().Include(e => e.VideoAccesses).FirstAsync(e => e.Id == enrollmentId);
            foreach (var access in enrollment.VideoAccesses)
                access.Decrement();
            await db.SaveChangesAsync();
            return 0;
        });

    private Task ExpireEnrollmentAsync(Guid enrollmentId)
        => factory.QueryDbAsync(db => db.Enrollments.IgnoreQueryFilters()
            .Where(e => e.Id == enrollmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ExpiresAtUtc, DateTimeOffset.UtcNow.AddDays(-1))));

    private Task SetExpiryAsync(Guid enrollmentId, DateTimeOffset when)
        => factory.QueryDbAsync(db => db.Enrollments.IgnoreQueryFilters()
            .Where(e => e.Id == enrollmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ExpiresAtUtc, when)));

    private Task SetEnrolledAtAsync(Guid enrollmentId, DateTimeOffset when)
        => factory.QueryDbAsync(db => db.Enrollments.IgnoreQueryFilters()
            .Where(e => e.Id == enrollmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.EnrolledAtUtc, when)));

    private Task<Guid> EnrollmentIdAsync(Guid studentId, Guid sessionId)
        => factory.QueryDbAsync(db => db.Enrollments.IgnoreQueryFilters()
            .Where(e => e.StudentId == studentId && e.SessionId == sessionId && e.Status != EnrollmentStatus.Refunded)
            .Select(e => e.Id)
            .FirstAsync());

    private Task<List<Guid>> VideoIdsAsync(Guid sessionId)
        => factory.QueryDbAsync(db => db.SessionVideos
            .Where(v => v.SessionId == sessionId).OrderBy(v => v.Order).Select(v => v.Id).ToListAsync());

    private Task<int> AuditCountAsync(Guid tenantId)
        => factory.QueryDbAsync(db => db.AuditEntries.CountAsync(a => a.TenantId == tenantId));

    private async Task RefundAsync(HttpClient teacher, Guid enrollmentId)
        => (await teacher.PostAsJsonAsync(
                $"/api/enrollments/{enrollmentId}/refund", new RefundRequestBody("changed mind"), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    // ── §E.4 onboarding (no enrollments) ─────────────────────────────────────
    [Fact]
    public async Task Plan_is_an_onboarding_redeem_step_when_the_caller_has_no_enrollments()
    {
        var (tenant, _, _, _, student) = await SetupAsync();

        var plan = await GetPlanAsync(factory.CreateClientForStudent(tenant, student.Id));

        plan.Focus.Should().BeNull();
        plan.Steps.Should().ContainSingle();
        var step = plan.Steps[0];
        step.Kind.Should().Be("Redeem");
        step.Title.Should().Be("Redeem a code");
        step.Action.Type.Should().Be("Redeem");
        step.Action.Route.Should().BeNull();
        plan.TotalSteps.Should().Be(1);
        plan.CompletedSteps.Should().Be(0);
        plan.OverdueSteps.Should().Be(0);
        plan.Kpis.Should().Be(new MyPlanKpisResponse(0, 0, 0, 0, 0));
        plan.RecentlyEnrolled.Should().BeEmpty();
        plan.IsoWeek.Should().MatchRegex(@"^\d{4}-W\d{2}$");
        plan.WeekEndUtc.Should().BeAfter(plan.WeekStartUtc);
    }

    // ── §E.3 focus: quiz-gated session, videos blocked until passed ───────────
    [Fact]
    public async Task Focus_is_the_quiz_gated_session_with_videos_blocked_until_the_quiz_passes()
    {
        var ctx = await factory.SetupGatedQuizAsync();
        // Complete the prerequisite A so the gated B is the sole active-incomplete focus.
        await SpendAllViewsAsync(await EnrollmentIdAsync(ctx.StudentId, ctx.SourceSessionId));

        var plan = await GetPlanAsync(ctx.Student);

        plan.Focus.Should().NotBeNull();
        plan.Focus!.SessionId.Should().Be(ctx.GatedSessionId);

        var quiz = plan.Steps.Single(s => s.Kind == "Quiz");
        quiz.Status.Should().Be("Pending");
        quiz.Blocked.Should().BeFalse();
        quiz.Title.Should().Be("Pass the gating quiz");
        quiz.Action.Type.Should().Be("Navigate");
        quiz.Action.Route.Should().Be($"/sessions/{ctx.GatedSessionId}");

        var videos = plan.Steps.Single(s => s.Kind == "Videos");
        videos.Status.Should().Be("Pending");
        videos.Blocked.Should().BeTrue();
        videos.BlockedReason.Should().Be("Pass the quiz to unlock the videos");
    }

    // ── §E.1/§E.3 focus: partway videos ──────────────────────────────────────
    [Fact]
    public async Task Focus_videos_step_reflects_partway_progress()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);
        await SpendViewsAsync(enrollment.Id, 1);

        var plan = await GetPlanAsync(studentClient);

        plan.Focus!.SessionId.Should().Be(session.Id);
        plan.Focus.ProgressPercent.Should().Be(50);
        var videos = plan.Steps.Single(s => s.Kind == "Videos");
        videos.Status.Should().Be("Pending");
        videos.Progress.Should().Be(new MyPlanProgressResponse(1, 2));
        videos.Action.Label.Should().Be("Continue");
        plan.Steps.Should().NotContain(s => s.Kind == "Assignment");   // no question bank → no assignment
    }

    // ── §E.3 focus: assignment incomplete ────────────────────────────────────
    [Fact]
    public async Task Focus_includes_a_pending_assignment_step_when_the_assignment_is_incomplete()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2);
        await EnrollAsync(studentClient, teacher, session.Id);

        var plan = await GetPlanAsync(studentClient);

        plan.Focus!.SessionId.Should().Be(session.Id);
        var assignment = plan.Steps.Single(s => s.Kind == "Assignment");
        assignment.Status.Should().Be("Pending");
        assignment.Title.Should().Be("Finish your assignment");
        assignment.Progress.Should().Be(new MyPlanProgressResponse(0, 2));
        assignment.Action.Label.Should().Be("Open");
    }

    // ── §E.4 all-done → roll-forward Redeem ──────────────────────────────────
    [Fact]
    public async Task Plan_rolls_forward_to_a_redeem_step_when_the_focus_session_is_fully_complete()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);
        await studentClient.CompleteAssignmentCorrectlyAsync(session.Id);
        await SpendAllViewsAsync(enrollment.Id);

        var plan = await GetPlanAsync(studentClient);

        plan.Focus.Should().BeNull();
        plan.Steps.Should().ContainSingle();
        plan.Steps[0].Kind.Should().Be("Redeem");
        plan.Steps[0].Title.Should().Be("Ready for your next session");
        plan.Kpis.CompletedSessions.Should().Be(1);
        plan.Kpis.ActiveSessions.Should().Be(0);   // completed → not "active"
    }

    // ── §B/§E.3 focus expiring soon ──────────────────────────────────────────
    [Fact]
    public async Task Focus_is_marked_expiring_soon_within_the_window()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);
        await SetExpiryAsync(enrollment.Id, DateTimeOffset.UtcNow.AddDays(5));

        var plan = await GetPlanAsync(studentClient);

        plan.Focus!.DueState.Should().Be("ExpiringSoon");
        plan.Focus.IsExpired.Should().BeFalse();
        plan.Focus.ExpiresInDays.Should().BeInRange(4, 5);
        plan.Steps.Single(s => s.Kind == "Videos").DueState.Should().Be("ExpiringSoon");
    }

    // ── §E.3.5 secondary nudges, capped at two ───────────────────────────────
    [Fact]
    public async Task Secondary_expiry_nudges_are_capped_at_two_and_total_steps_stay_within_seven()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var now = DateTimeOffset.UtcNow;

        // Four active, incomplete, expiring-soon sessions. Focus = soonest (3d); the next two (5d, 7d) become
        // nudges; the 9d one is dropped by the ≤ 2 cap. The 7-step structural cap also holds.
        var sooner = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);
        var nudgeA = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);
        var nudgeB = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);
        var dropped = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);

        await SetExpiryAsync((await EnrollAsync(studentClient, teacher, sooner.Id)).Id, now.AddDays(3));
        await SetExpiryAsync((await EnrollAsync(studentClient, teacher, nudgeA.Id)).Id, now.AddDays(5));
        await SetExpiryAsync((await EnrollAsync(studentClient, teacher, nudgeB.Id)).Id, now.AddDays(7));
        await SetExpiryAsync((await EnrollAsync(studentClient, teacher, dropped.Id)).Id, now.AddDays(9));

        var plan = await GetPlanAsync(studentClient);

        plan.Focus!.SessionId.Should().Be(sooner.Id);
        var nudges = plan.Steps.Where(s => s.SessionId != sooner.Id).ToList();
        nudges.Should().HaveCount(2);
        nudges.Select(s => s.SessionId).Should().BeEquivalentTo(new[] { nudgeA.Id, nudgeB.Id });
        nudges.Should().OnlyContain(s => s.DueState == "ExpiringSoon");
        plan.Steps.Count.Should().BeLessThanOrEqualTo(7);
    }

    // ── §E.4 expired-only → Expired assignment + Renew Redeem ────────────────
    [Fact]
    public async Task Expired_only_plan_surfaces_the_open_assignment_and_a_renew_redeem()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);
        await ExpireEnrollmentAsync(enrollment.Id);

        var plan = await GetPlanAsync(studentClient);

        plan.Focus.Should().BeNull();
        var assignment = plan.Steps.Single(s => s.Kind == "Assignment");
        assignment.Status.Should().Be("Pending");
        assignment.DueState.Should().Be("Expired");
        assignment.SessionId.Should().Be(session.Id);
        var redeem = plan.Steps.Single(s => s.Kind == "Redeem");
        redeem.Title.Should().Be("Renew access");
        plan.OverdueSteps.Should().Be(1);   // the expired, incomplete assignment
    }

    // ── §E.2 prerequisite-depth tie-break ────────────────────────────────────
    [Fact]
    public async Task Focus_tie_breaks_on_prerequisite_depth_so_the_chain_finishes_in_order()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        // No-expiry sessions (validity 0) so ExpiresAtUtc is null for both → the primary keys tie.
        var a = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, validityDays: 0, videoCount: 1);
        var b = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, validityDays: 0, videoCount: 1);
        var aEnroll = await EnrollAsync(studentClient, teacher, a.Id);
        var bEnroll = await EnrollAsync(studentClient, teacher, b.Id);
        await factory.SetSessionPrerequisiteAsync(b.Id, a.Id);   // B depends on A → A is earlier in the chain

        // Identical enrolled-at → the prerequisite-depth tie-break decides (A: depth 0, B: depth 1).
        var pinned = DateTimeOffset.UtcNow.AddDays(-1);
        await SetEnrolledAtAsync(aEnroll.Id, pinned);
        await SetEnrolledAtAsync(bEnroll.Id, pinned);

        (await GetPlanAsync(studentClient)).Focus!.SessionId.Should().Be(a.Id);
    }

    // ── §E.5 KPIs ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Kpis_are_summed_over_the_active_set_with_completed_over_the_whole_set()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var inProgress = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var completed = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var expired = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);

        var inProgressEnrollment = await EnrollAsync(studentClient, teacher, inProgress.Id);
        var completedEnrollment = await EnrollAsync(studentClient, teacher, completed.Id);
        var expiredEnrollment = await EnrollAsync(studentClient, teacher, expired.Id);

        await SpendViewsAsync(inProgressEnrollment.Id, 1);   // 1/2 → InProgress, active
        await SpendViewsAsync(completedEnrollment.Id, 2);    // 2/2 → Completed, active
        await ExpireEnrollmentAsync(expiredEnrollment.Id);   // excluded from the active sums

        var plan = await GetPlanAsync(studentClient);

        // active = {inProgress, completed}; videos 1+2 of 2+2 = 3/4 (75%). active-and-not-completed = inProgress.
        plan.Kpis.ActiveSessions.Should().Be(1);
        plan.Kpis.VideosWatched.Should().Be(3);
        plan.Kpis.VideosTotal.Should().Be(4);
        plan.Kpis.OverallProgressPercent.Should().Be(75);
        plan.Kpis.CompletedSessions.Should().Be(1);
        plan.RecentlyEnrolled.Should().HaveCount(3);
    }

    // ── §E.5 recently-enrolled, newest first, capped at five ─────────────────
    [Fact]
    public async Task RecentlyEnrolled_lists_newest_first_capped_at_five()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var now = DateTimeOffset.UtcNow;

        var sessions = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);
            var enrollment = await EnrollAsync(studentClient, teacher, session.Id);
            await SetEnrolledAtAsync(enrollment.Id, now.AddDays(-6 + i));   // ascending enrolled-at
            sessions.Add(session.Id);
        }

        var plan = await GetPlanAsync(studentClient);

        // Newest five, DESC by enrolled-at: indexes 5,4,3,2,1 (the oldest, index 0, is dropped).
        plan.RecentlyEnrolled.Select(r => r.SessionId)
            .Should().Equal(sessions[5], sessions[4], sessions[3], sessions[2], sessions[1]);
    }

    // ── §C/§D cache invalidation through the real engines (no TTL wait) ──────
    [Fact]
    public async Task Plan_reflects_a_fresh_enrollment_then_a_refund_without_waiting_for_ttl()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        (await GetPlanAsync(studentClient)).Focus.Should().BeNull();   // warm: onboarding

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);   // EnrollmentCreated → invalidate

        (await GetPlanAsync(studentClient)).Focus!.SessionId.Should().Be(session.Id);

        await RefundAsync(teacher, enrollment.Id);   // EnrollmentRefunded → invalidate

        (await GetPlanAsync(studentClient)).Focus.Should().BeNull();   // back to onboarding
    }

    [Fact]
    public async Task Plan_reflects_watching_a_video_through_the_real_gate_without_waiting_for_ttl()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var videoId = (await VideoIdsAsync(session.Id))[0];
        await factory.SeedReadyHlsAsync(videoId);
        await EnrollAsync(studentClient, teacher, session.Id);

        var before = await GetPlanAsync(studentClient);   // warms the cache at 0 watched
        before.Focus!.ProgressPercent.Should().Be(0);
        before.Steps.Single(s => s.Kind == "Videos").Progress.Should().Be(new MyPlanProgressResponse(0, 2));

        // The 5C playback gate decrements inline and raises no event — the inline InvalidateAsync must still drop it.
        (await studentClient.PostAsync($"/api/me/videos/{videoId}/playback", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await GetPlanAsync(studentClient);
        after.Focus!.ProgressPercent.Should().Be(50);
        after.Steps.Single(s => s.Kind == "Videos").Progress.Should().Be(new MyPlanProgressResponse(1, 2));
    }

    [Fact]
    public async Task Plan_reflects_completing_the_assignment_without_waiting_for_ttl()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        await EnrollAsync(studentClient, teacher, session.Id);

        (await GetPlanAsync(studentClient)).Steps.Single(s => s.Kind == "Assignment").Status.Should().Be("Pending");

        // Answering the last question auto-grades + raises AssignmentGraded (and the inline drop also fires).
        await studentClient.CompleteAssignmentCorrectlyAsync(session.Id);

        (await GetPlanAsync(studentClient)).Steps.Single(s => s.Kind == "Assignment").Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Plan_reflects_passing_the_gating_quiz_without_waiting_for_ttl()
    {
        var ctx = await factory.SetupGatedQuizAsync();
        await SpendAllViewsAsync(await EnrollmentIdAsync(ctx.StudentId, ctx.SourceSessionId));   // B is the focus

        var before = await GetPlanAsync(ctx.Student);
        before.Focus!.SessionId.Should().Be(ctx.GatedSessionId);
        before.Steps.Single(s => s.Kind == "Videos").Blocked.Should().BeTrue();

        // Pass the quiz through the real engine → QuizGraded → invalidate.
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: attempt.Questions.Count);
        await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        var after = await GetPlanAsync(ctx.Student);
        after.Steps.Single(s => s.Kind == "Quiz").Status.Should().Be("Completed");
        after.Steps.Single(s => s.Kind == "Videos").Blocked.Should().BeFalse();
    }

    // ── Cross-tenant isolation (NFR-SEC-010) ─────────────────────────────────
    [Fact]
    public async Task Plan_is_isolated_to_the_callers_tenant()
    {
        var (tenantA, gradeA, _, specA, studentA) = await SetupAsync();
        var teacherA = factory.CreateClientFor(StaffRole.Teacher, tenantA);
        var sessionA = await factory.SeedSessionWithContentAsync(tenantA, gradeA, specA, videoCount: 1);
        await EnrollAsync(factory.CreateClientForStudent(tenantA, studentA.Id), teacherA, sessionA.Id);

        var (tenantB, gradeB, _, specB, studentB) = await SetupAsync();
        var teacherB = factory.CreateClientFor(StaffRole.Teacher, tenantB);
        var sessionB = await factory.SeedSessionWithContentAsync(tenantB, gradeB, specB, videoCount: 1);
        await EnrollAsync(factory.CreateClientForStudent(tenantB, studentB.Id), teacherB, sessionB.Id);

        var planB = await GetPlanAsync(factory.CreateClientForStudent(tenantB, studentB.Id));

        planB.Focus!.SessionId.Should().Be(sessionB.Id);
        planB.RecentlyEnrolled.Select(r => r.SessionId).Should().Contain(sessionB.Id).And.NotContain(sessionA.Id);
    }

    // ── §F read is not audited ───────────────────────────────────────────────
    [Fact]
    public async Task Plan_read_is_not_audited()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await EnrollAsync(studentClient, teacher,
            (await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1)).Id);

        var before = await AuditCountAsync(tenant);
        await GetPlanAsync(studentClient);
        (await AuditCountAsync(tenant)).Should().Be(before);
    }

    // ── §A.2 role gating ─────────────────────────────────────────────────────
    [Fact]
    public async Task Plan_read_is_student_only()
    {
        var (tenant, _, _, _, student) = await SetupAsync();

        (await factory.CreateClient().GetAsync("/api/me/plan"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClientFor(StaffRole.Teacher, tenant).GetAsync("/api/me/plan"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClientForStudent(tenant, student.Id).GetAsync("/api/me/plan"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
