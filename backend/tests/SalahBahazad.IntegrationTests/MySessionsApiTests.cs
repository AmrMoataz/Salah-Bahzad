using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The student-portal S3 My-Sessions reads (contract §A/§B/§C, FR-STU-SES-001..004): the enrolled-set scope
/// (Active incl. past-expiry, excluding Refunded), DESC ordering + shape, the derived progress (§E.1), the derived
/// isExpired + completion state + <c>?state=</c> filter (§E.2), the session-detail playlist/material/assignment/quiz
/// shape, the per-video <c>lockState</c> precedence mirroring the 5C gate (§E.3), the gate banner (§E.4), the 404
/// IDOR/tenant boundary on detail + material, the material signed-URL read (200 active / 200 expired / 404 refunded /
/// not-audited), cross-tenant isolation, and 401/403/200 role gating.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MySessionsApiTests(SalahBahazadApiFactory factory)
{
    private async Task<(Guid Tenant, Guid GradeId, Guid SubjectId, Guid SpecId, StudentEntity Student)> SetupAsync()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, subjectId, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        return (tenant, gradeId, subjectId, specId, student);
    }

    private async Task<List<MySessionResponse>> GetMySessionsAsync(HttpClient client, string query = "")
        => (await client.GetFromJsonAsync<List<MySessionResponse>>(
            $"/api/me/sessions{query}", TestJson.Options))!;

    private static async Task<MySessionDetailResponse> ReadDetailAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<MySessionDetailResponse>(TestJson.Options))!;

    /// <summary>Enrolls the student in a session via the real redeem path and returns the enrollment.</summary>
    private async Task<EnrollmentResult> EnrollAsync(HttpClient student, HttpClient teacher, Guid sessionId)
        => await student.RedeemAsync(await teacher.GenerateOneSerialAsync(sessionId));

    /// <summary>Back-dates an enrollment's expiry into the past without touching its status (the writer never flips
    /// Status to Expired) — so the derived-isExpired path (§E.2) can be exercised.</summary>
    private Task ExpireEnrollmentAsync(Guid enrollmentId)
        => factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters()
            .Where(e => e.Id == enrollmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ExpiresAtUtc, DateTimeOffset.UtcNow.AddDays(-1))));

    private Task SetExpiryAsync(Guid enrollmentId, DateTimeOffset when)
        => factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters()
            .Where(e => e.Id == enrollmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ExpiresAtUtc, when)));

    private Task SetEnrolledAtAsync(Guid enrollmentId, DateTimeOffset when)
        => factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters()
            .Where(e => e.Id == enrollmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.EnrolledAtUtc, when)));

    /// <summary>Spends one view on the first <paramref name="videos"/> distinct video counters — making each count
    /// toward "videosWatched" (AccessRemaining &lt; AccessAllowed), the same source the 5C gate decrements.</summary>
    private Task SpendViewsAsync(Guid enrollmentId, int videos)
        => factory.QueryDbAsync<int>(async db =>
        {
            var enrollment = await db.Enrollments
                .IgnoreQueryFilters()
                .Include(e => e.VideoAccesses)
                .FirstAsync(e => e.Id == enrollmentId);
            foreach (var access in enrollment.VideoAccesses.OrderBy(a => a.VideoId).Take(videos))
                access.Decrement();
            await db.SaveChangesAsync();
            return videos;
        });

    private Task<int> AuditCountAsync(Guid tenantId)
        => factory.QueryDbAsync(db => db.AuditEntries.CountAsync(a => a.TenantId == tenantId));

    private async Task RefundAsync(HttpClient teacher, Guid enrollmentId)
        => (await teacher.PostAsJsonAsync(
                $"/api/enrollments/{enrollmentId}/refund", new RefundRequestBody("changed mind"), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    private Task<List<Guid>> VideoIdsAsync(Guid sessionId)
        => factory.QueryDbAsync(db => db.SessionVideos
            .Where(v => v.SessionId == sessionId)
            .OrderBy(v => v.Order)
            .Select(v => v.Id)
            .ToListAsync());

    // ── §A scope + shape + ordering ──────────────────────────────────────────
    [Fact]
    public async Task MySessions_returns_active_and_expired_excludes_refunded_with_shape()
    {
        var (tenant, gradeId, subjectId, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var active = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var expired = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var refunded = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);

        var activeEnrollment = await EnrollAsync(studentClient, teacher, active.Id);
        var expiredEnrollment = await EnrollAsync(studentClient, teacher, expired.Id);
        var refundedEnrollment = await EnrollAsync(studentClient, teacher, refunded.Id);

        await ExpireEnrollmentAsync(expiredEnrollment.Id);  // Active row, past expiry → derived isExpired
        await RefundAsync(teacher, refundedEnrollment.Id);

        var result = await GetMySessionsAsync(studentClient);

        // Refunded is absent; active + past-expiry are both present.
        result.Select(x => x.Id).Should().BeEquivalentTo([active.Id, expired.Id]);

        var card = result.Single(x => x.Id == active.Id);
        card.EnrollmentId.Should().Be(activeEnrollment.Id);
        card.Title.Should().Be(active.Title);
        card.GradeName.Should().NotBeNullOrWhiteSpace();
        card.SubjectName.Should().NotBeNullOrWhiteSpace();
        card.SpecializationName.Should().NotBeNullOrWhiteSpace();
        card.ThumbnailUrl.Should().BeNull();            // no thumbnail seeded
        card.VideoCount.Should().Be(2);
        card.VideosWatched.Should().Be(0);
        card.ProgressPercent.Should().Be(0);
        card.IsExpired.Should().BeFalse();
        card.State.Should().Be("NotStarted");

        result.Single(x => x.Id == expired.Id).IsExpired.Should().BeTrue();

        // Derived, not stored: the back-dated enrollment is still Active in the DB.
        var status = await factory.QueryDbAsync(db => db.Enrollments.IgnoreQueryFilters()
            .Where(e => e.Id == expiredEnrollment.Id).Select(e => e.Status).FirstAsync());
        status.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task MySessions_are_ordered_by_enrolled_at_desc()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var first = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var second = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var firstEnrollment = await EnrollAsync(studentClient, teacher, first.Id);
        var secondEnrollment = await EnrollAsync(studentClient, teacher, second.Id);

        // Pin the enrolled-at instants so the order is deterministic (second is newer).
        await SetEnrolledAtAsync(firstEnrollment.Id, DateTimeOffset.UtcNow.AddDays(-2));
        await SetEnrolledAtAsync(secondEnrollment.Id, DateTimeOffset.UtcNow.AddDays(-1));

        (await GetMySessionsAsync(studentClient)).Select(x => x.Id).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public async Task MySessions_is_empty_when_the_caller_has_no_enrollments()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);

        (await GetMySessionsAsync(factory.CreateClientForStudent(tenant, student.Id))).Should().BeEmpty();
    }

    // ── §E.1 progress derivation ─────────────────────────────────────────────
    [Fact]
    public async Task Progress_is_derived_from_spent_views()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);

        var before = (await GetMySessionsAsync(studentClient)).Single(x => x.Id == session.Id);
        before.VideosWatched.Should().Be(0);
        before.ProgressPercent.Should().Be(0);
        before.State.Should().Be("NotStarted");

        await SpendViewsAsync(enrollment.Id, 1);
        var half = (await GetMySessionsAsync(studentClient)).Single(x => x.Id == session.Id);
        half.VideosWatched.Should().Be(1);
        half.ProgressPercent.Should().Be(50);
        half.State.Should().Be("InProgress");

        await SpendViewsAsync(enrollment.Id, 2);
        var done = (await GetMySessionsAsync(studentClient)).Single(x => x.Id == session.Id);
        done.VideosWatched.Should().Be(2);
        done.ProgressPercent.Should().Be(100);
        done.State.Should().Be("Completed");
    }

    [Fact]
    public async Task Progress_is_zero_for_a_session_with_no_videos()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 0);
        await EnrollAsync(studentClient, teacher, session.Id);

        var card = (await GetMySessionsAsync(studentClient)).Single(x => x.Id == session.Id);
        card.VideoCount.Should().Be(0);
        card.ProgressPercent.Should().Be(0);
        card.State.Should().Be("NotStarted");
    }

    // ── §A.1/§E.2 the ?state= filter ─────────────────────────────────────────
    [Fact]
    public async Task State_filter_narrows_by_completion_and_expiry()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        // InProgress (1 of 2 watched), Completed (all watched), ExpiringSoon (≤14d), Expired (past).
        var inProgress = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var completed = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        var expiringSoon = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var expired = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);

        var inProgressEnrollment = await EnrollAsync(studentClient, teacher, inProgress.Id);
        var completedEnrollment = await EnrollAsync(studentClient, teacher, completed.Id);
        var expiringSoonEnrollment = await EnrollAsync(studentClient, teacher, expiringSoon.Id);
        var expiredEnrollment = await EnrollAsync(studentClient, teacher, expired.Id);

        await SpendViewsAsync(inProgressEnrollment.Id, 1);
        await SpendViewsAsync(completedEnrollment.Id, 2);
        await SetExpiryAsync(expiringSoonEnrollment.Id, DateTimeOffset.UtcNow.AddDays(5));
        await ExpireEnrollmentAsync(expiredEnrollment.Id);

        (await GetMySessionsAsync(studentClient, "?state=InProgress")).Select(x => x.Id)
            .Should().Equal(inProgress.Id);
        (await GetMySessionsAsync(studentClient, "?state=Completed")).Select(x => x.Id)
            .Should().Equal(completed.Id);
        (await GetMySessionsAsync(studentClient, "?state=ExpiringSoon")).Select(x => x.Id)
            .Should().Equal(expiringSoon.Id);
        (await GetMySessionsAsync(studentClient, "?state=Expired")).Select(x => x.Id)
            .Should().Equal(expired.Id);

        // An unrecognised value is ignored → the whole set is returned (lenient parse, §A.1).
        (await GetMySessionsAsync(studentClient, "?state=bogus")).Should().HaveCount(4);
    }

    // ── §B.1 detail happy path ───────────────────────────────────────────────
    [Fact]
    public async Task Detail_returns_ordered_playlist_materials_and_assignment()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        // A session with a question bank (so an assignment is generated) and two videos.
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var videoIds = await VideoIdsAsync(session.Id);
        await factory.SeedReadyHlsAsync(videoIds[0]);   // first video Ready; second stays Pending

        var mat1 = await factory.SeedMaterialAsync(session.Id, "first.pdf");
        var mat2 = await factory.SeedMaterialAsync(session.Id, "second.csv", "text/csv");
        // Pin material timestamps so the CreatedAtUtc ordering is deterministic.
        await factory.QueryDbAsync(db => db.SessionMaterials.Where(m => m.Id == mat1.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAtUtc, DateTimeOffset.UtcNow.AddMinutes(-2))));
        await factory.QueryDbAsync(db => db.SessionMaterials.Where(m => m.Id == mat2.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1))));

        await EnrollAsync(studentClient, teacher, session.Id);

        var detail = await ReadDetailAsync(await studentClient.GetAsync($"/api/me/sessions/{session.Id}"));

        detail.Id.Should().Be(session.Id);
        detail.GradeName.Should().NotBeNullOrWhiteSpace();
        detail.SubjectName.Should().NotBeNullOrWhiteSpace();
        detail.SpecializationName.Should().NotBeNullOrWhiteSpace();
        detail.GateState.Should().Be("Open");
        detail.HasGatingQuiz.Should().BeFalse();

        // Videos ordered by Order; first is Ready+Playable, second is Pending+NotReady.
        detail.Videos.Select(v => v.Id).Should().Equal(videoIds[0], videoIds[1]);
        detail.Videos[0].ProcessingStatus.Should().Be("Ready");
        detail.Videos[0].AccessAllowed.Should().Be(3);
        detail.Videos[0].AccessRemaining.Should().Be(3);
        detail.Videos[0].LockState.Should().Be("Playable");
        detail.Videos[1].ProcessingStatus.Should().Be("Pending");
        detail.Videos[1].LockState.Should().Be("NotReady");

        // Materials ordered by CreatedAtUtc; names-only shape.
        detail.Materials.Select(m => m.Id).Should().Equal(mat1.Id, mat2.Id);
        detail.Materials[0].Kind.Should().Be("PDF");
        detail.Materials[1].Kind.Should().Be("CSV");

        // Assignment populated; quiz null (not gated).
        detail.Assignment.Should().NotBeNull();
        detail.Assignment!.Status.Should().Be("InProgress");
        detail.Assignment.QuestionCount.Should().Be(1);
        detail.Assignment.MaxMarks.Should().Be(1);
        detail.Assignment.ScoreMarks.Should().BeNull();
        detail.Quiz.Should().BeNull();
    }

    // ── §E.3 per-video lockState precedence ──────────────────────────────────
    [Fact]
    public async Task Detail_expired_session_locks_every_video()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 2);
        foreach (var id in await VideoIdsAsync(session.Id))
            await factory.SeedReadyHlsAsync(id);                       // Ready, yet expiry must still lock them
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);
        await ExpireEnrollmentAsync(enrollment.Id);

        var detail = await ReadDetailAsync(await studentClient.GetAsync($"/api/me/sessions/{session.Id}"));

        detail.IsExpired.Should().BeTrue();
        detail.GateState.Should().Be("Expired");
        detail.Videos.Should().OnlyContain(v => v.LockState == "Expired");
    }

    [Fact]
    public async Task Detail_exhausted_video_is_marked_when_no_views_remain()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, videoCount: 1, accessPerVideo: 1);
        var videoId = (await VideoIdsAsync(session.Id))[0];
        await factory.SeedReadyHlsAsync(videoId);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);

        // Ready + views remaining → Playable.
        var playable = await ReadDetailAsync(await studentClient.GetAsync($"/api/me/sessions/{session.Id}"));
        playable.Videos.Single().LockState.Should().Be("Playable");

        await SpendViewsAsync(enrollment.Id, 1);   // 1 → 0

        var exhausted = await ReadDetailAsync(await studentClient.GetAsync($"/api/me/sessions/{session.Id}"));
        exhausted.Videos.Single().AccessRemaining.Should().Be(0);
        exhausted.Videos.Single().LockState.Should().Be("Exhausted");
    }

    // ── §E.3/§E.4 quiz gate ──────────────────────────────────────────────────
    [Fact]
    public async Task Detail_quiz_gate_locks_videos_until_passed_then_opens()
    {
        var ctx = await factory.SetupGatedQuizAsync();
        var videoId = (await VideoIdsAsync(ctx.GatedSessionId))[0];
        await factory.SeedReadyHlsAsync(videoId);

        // Before passing: the gate banner is QuizRequired and every video is QuizLocked.
        var locked = await ReadDetailAsync(await ctx.Student.GetAsync($"/api/me/sessions/{ctx.GatedSessionId}"));
        locked.HasGatingQuiz.Should().BeTrue();
        locked.QuizPassed.Should().BeFalse();
        locked.MinPassPercent.Should().Be(60);
        locked.GateState.Should().Be("QuizRequired");
        locked.Videos.Should().OnlyContain(v => v.LockState == "QuizLocked");
        locked.Quiz.Should().NotBeNull();
        locked.Quiz!.AttemptCount.Should().Be(3);
        locked.Quiz.QuestionCount.Should().Be(2);
        locked.Quiz.TimeLimitMinutes.Should().Be(30);

        // Pass the quiz (both correct → 100% ≥ 60).
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: attempt.Questions.Count);
        await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        var open = await ReadDetailAsync(await ctx.Student.GetAsync($"/api/me/sessions/{ctx.GatedSessionId}"));
        open.QuizPassed.Should().BeTrue();
        open.GateState.Should().Be("Open");
        open.Quiz!.Passed.Should().BeTrue();
        open.Quiz.BestPercent.Should().Be(100);
        open.Videos.Single(v => v.Id == videoId).LockState.Should().Be("Playable");
    }

    // ── §B.2 the 404 IDOR/tenant boundary ────────────────────────────────────
    [Fact]
    public async Task Detail_404_for_unknown_unenrolled_refunded_or_cross_tenant_session()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var unknownId = Guid.NewGuid();
        (await studentClient.GetAsync($"/api/me/sessions/{unknownId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Enrolled-in nothing: a real session the caller is not enrolled in → 404 (not the data).
        var notEnrolled = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        (await studentClient.GetAsync($"/api/me/sessions/{notEnrolled.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Refunded-only enrollment → 404.
        var refunded = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var refundedEnrollment = await EnrollAsync(studentClient, teacher, refunded.Id);
        await RefundAsync(teacher, refundedEnrollment.Id);
        (await studentClient.GetAsync($"/api/me/sessions/{refunded.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A session in another tenant → 404.
        var (tenantB, gradeB, _, specB, _) = await SetupAsync();
        var sessionB = await factory.SeedSessionWithContentAsync(tenantB, gradeB, specB);
        (await studentClient.GetAsync($"/api/me/sessions/{sessionB.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── §C material signed URL ───────────────────────────────────────────────
    [Fact]
    public async Task Material_url_resolves_while_active_and_expired_404_when_refunded_and_is_not_audited()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var material = await factory.SeedMaterialAsync(session.Id);
        var enrollment = await EnrollAsync(studentClient, teacher, session.Id);

        var url = $"/api/me/sessions/{session.Id}/materials/{material.Id}/url";

        // Active → 200 SignedUrlDto; the read writes no audit row.
        var auditBefore = await AuditCountAsync(tenant);
        var active = await studentClient.GetAsync(url);
        active.StatusCode.Should().Be(HttpStatusCode.OK);
        var signed = (await active.Content.ReadFromJsonAsync<SignedUrlResponse>(TestJson.Options))!;
        signed.Url.Should().NotBeNullOrWhiteSpace();
        signed.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
        (await AuditCountAsync(tenant)).Should().Be(auditBefore);   // not audited (§F)

        // Expired enrollment → materials stay reachable (FR-STU-SES-001).
        await ExpireEnrollmentAsync(enrollment.Id);
        (await studentClient.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);

        // A foreign material id on the enrolled session → 404.
        (await studentClient.GetAsync($"/api/me/sessions/{session.Id}/materials/{Guid.NewGuid()}/url"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Refunded enrollment → 404 (access reversed).
        await RefundAsync(teacher, enrollment.Id);
        (await studentClient.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Material_url_404_when_the_session_is_not_an_enrolled_one()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        // A material of a session the caller is not enrolled in → 404 (no IDOR via the material id).
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var material = await factory.SeedMaterialAsync(session.Id);

        (await studentClient.GetAsync($"/api/me/sessions/{session.Id}/materials/{material.Id}/url"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Cross-tenant isolation (NFR-SEC-010) ─────────────────────────────────
    [Fact]
    public async Task MySessions_are_isolated_to_the_callers_tenant()
    {
        var (tenantA, gradeA, _, specA, studentA) = await SetupAsync();
        var teacherA = factory.CreateClientFor(StaffRole.Teacher, tenantA);
        var sessionA = await factory.SeedSessionWithContentAsync(tenantA, gradeA, specA);
        await EnrollAsync(factory.CreateClientForStudent(tenantA, studentA.Id), teacherA, sessionA.Id);

        var (tenantB, gradeB, _, specB, studentB) = await SetupAsync();
        var teacherB = factory.CreateClientFor(StaffRole.Teacher, tenantB);
        var sessionB = await factory.SeedSessionWithContentAsync(tenantB, gradeB, specB);
        await EnrollAsync(factory.CreateClientForStudent(tenantB, studentB.Id), teacherB, sessionB.Id);

        var studentBClient = factory.CreateClientForStudent(tenantB, studentB.Id);
        (await GetMySessionsAsync(studentBClient)).Select(x => x.Id)
            .Should().Contain(sessionB.Id).And.NotContain(sessionA.Id);

        // And tenant A's session is a 404 to student B (not the data).
        (await studentBClient.GetAsync($"/api/me/sessions/{sessionA.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Role gating (§A.3/§B.2/§C) ───────────────────────────────────────────
    [Fact]
    public async Task MySessions_reads_are_student_only()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        await EnrollAsync(studentClient, teacher, session.Id);

        // Anonymous → 401.
        (await factory.CreateClient().GetAsync("/api/me/sessions"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Staff token → 403.
        (await factory.CreateClientFor(StaffRole.Teacher, tenant).GetAsync("/api/me/sessions"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClientFor(StaffRole.Teacher, tenant).GetAsync($"/api/me/sessions/{session.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Student token → 200.
        (await studentClient.GetAsync("/api/me/sessions")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await studentClient.GetAsync($"/api/me/sessions/{session.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
