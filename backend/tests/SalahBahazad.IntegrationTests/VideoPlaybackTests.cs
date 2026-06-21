using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Features.Videos.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The Phase 5C secure video-playback gate (FR-PLAT-VID-001..007), driven with a student JWT (no admin UI).
/// Proves: gate → decrement + audit (Student actor) → one-time handoff; redeem → per-playback signed manifest +
/// gated key URL with a fetchable segment URL; the key endpoint serves the bytes without decrementing; every
/// failure reason; one-time-code reuse → 410; default-deny + tenant isolation. Transcode is faked (no ffmpeg in
/// CI) but the HLS objects are real in MinIO, so the redeem's signed segment URL is actually fetched.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class VideoPlaybackTests(SalahBahazadApiFactory factory)
{
    private sealed record PlaybackContext(
        Guid Tenant, Guid StudentId, Guid SessionId, Guid VideoId, Guid EnrollmentId,
        HttpClient Student, HttpClient Teacher);

    private sealed record ProblemReasonResponse(string? Reason);

    /// <summary>Seeds an Active student enrolled in a published session with one (optionally Ready) HLS video.</summary>
    private async Task<PlaybackContext> SetupPlayableAsync(int accessPerVideo = 2, bool ready = true)
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);

        var session = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, videoCount: 1, accessPerVideo: accessPerVideo);
        var videoId = await factory.QueryDbAsync(async db =>
            await db.SessionVideos.Where(v => v.SessionId == session.Id).Select(v => v.Id).FirstAsync());

        if (ready)
            await factory.SeedReadyHlsAsync(videoId);

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var enrollment = await studentClient.RedeemAsync(serial);

        return new PlaybackContext(tenant, student.Id, session.Id, videoId, enrollment.Id, studentClient, teacher);
    }

    private Task<int> AccessRemainingAsync(Guid enrollmentId, Guid videoId)
        => factory.QueryDbAsync(async db =>
        {
            var enrollment = await db.Enrollments.IgnoreQueryFilters()
                .Include(e => e.VideoAccesses)
                .FirstAsync(e => e.Id == enrollmentId);
            return enrollment.VideoAccesses.Single(a => a.VideoId == videoId).AccessRemaining;
        });

    private static async Task<string?> ReasonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ProblemReasonResponse>(TestJson.Options))?.Reason;

    [Fact]
    public async Task Gate_decrements_audits_then_redeem_and_key_serve_playback()
    {
        var ctx = await SetupPlayableAsync(accessPerVideo: 2);

        // ── Gate ──
        var gate = await ctx.Student.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null);
        gate.StatusCode.Should().Be(HttpStatusCode.OK);
        var handoff = (await gate.Content.ReadFromJsonAsync<PlaybackHandoffDto>(TestJson.Options))!;
        handoff.HandoffCode.Should().NotBeNullOrWhiteSpace();

        (await AccessRemainingAsync(ctx.EnrollmentId, ctx.VideoId)).Should().Be(1); // 2 → 1

        var audit = await factory.LatestAuditAsync(ctx.Tenant, "SessionVideo", "VideoPlaybackStarted");
        audit.Should().NotBeNull();
        audit!.ActorType.Should().Be("Student"); // FR-PLAT-VID-002

        // ── Redeem ──
        var redeem = await ctx.Student.PostAsJsonAsync(
            "/api/me/videos/playback/redeem", new { handoffCode = handoff.HandoffCode }, TestJson.Options);
        redeem.StatusCode.Should().Be(HttpStatusCode.OK);
        var manifest = (await redeem.Content.ReadFromJsonAsync<PlaybackManifestDto>(TestJson.Options))!;

        manifest.ManifestContent.Should().Contain("#EXTM3U");
        manifest.ManifestContent.Should().NotContain(HlsConventions.KeyUriPlaceholder); // placeholder replaced
        manifest.KeyUrl.Should().Contain($"/api/me/videos/{ctx.VideoId}/hls.key");
        manifest.ManifestContent.Should().Contain(manifest.KeyUrl);

        // The signed segment URL actually serves bytes from MinIO (short-lived, non-replayable).
        var segmentUrl = manifest.ManifestContent
            .Split('\n').First(l => l.StartsWith("http", StringComparison.OrdinalIgnoreCase));
        using var http = new HttpClient();
        (await http.GetAsync(segmentUrl)).StatusCode.Should().Be(HttpStatusCode.OK);

        // ── One-time code: reuse → 410 ──
        var reuse = await ctx.Student.PostAsJsonAsync(
            "/api/me/videos/playback/redeem", new { handoffCode = handoff.HandoffCode }, TestJson.Options);
        reuse.StatusCode.Should().Be(HttpStatusCode.Gone);

        // ── Key endpoint: 16 bytes, no decrement ──
        var keyResponse = await ctx.Student.GetAsync($"/api/me/videos/{ctx.VideoId}/hls.key");
        keyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await keyResponse.Content.ReadAsByteArrayAsync()).Should().Equal(FakeMediaTranscoder.Key);
        (await AccessRemainingAsync(ctx.EnrollmentId, ctx.VideoId)).Should().Be(1); // key fetch never decrements
    }

    [Fact]
    public async Task Watching_a_video_counts_toward_attendance()
    {
        var ctx = await SetupPlayableAsync(accessPerVideo: 2);

        var before = (await ctx.Teacher.GetFromJsonAsync<PagedStudentAttendance>(
            $"/api/attendance/students/{ctx.StudentId}", TestJson.Options))!;
        before.Items.Single(r => r.SessionId == ctx.SessionId).VideosWatched.Should().Be(0);

        (await ctx.Student.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await ctx.Teacher.GetFromJsonAsync<PagedStudentAttendance>(
            $"/api/attendance/students/{ctx.StudentId}", TestJson.Options))!;
        var row = after.Items.Single(r => r.SessionId == ctx.SessionId);
        row.VideosWatched.Should().Be(1);  // one distinct video now has a spent view
        row.VideosTotal.Should().Be(1);
    }

    [Fact]
    public async Task Gate_blocks_when_no_views_remain()
    {
        var ctx = await SetupPlayableAsync(accessPerVideo: 1);

        (await ctx.Student.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null))
            .StatusCode.Should().Be(HttpStatusCode.OK); // 1 → 0

        var blocked = await ctx.Student.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null);
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReasonAsync(blocked)).Should().Be("no_views_remaining");
    }

    [Fact]
    public async Task Gate_blocks_a_video_still_processing()
    {
        var ctx = await SetupPlayableAsync(ready: false);

        var response = await ctx.Student.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReasonAsync(response)).Should().Be("not_ready");
    }

    [Fact]
    public async Task Gate_blocks_a_student_who_is_not_enrolled()
    {
        var ctx = await SetupPlayableAsync();
        var stranger = factory.CreateClientForStudent(ctx.Tenant, Guid.NewGuid());

        var response = await stranger.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReasonAsync(response)).Should().Be("not_enrolled");
    }

    [Fact]
    public async Task Gate_blocks_until_the_prerequisite_quiz_is_passed()
    {
        var ctx = await factory.SetupGatedQuizAsync();
        var videoId = await factory.QueryDbAsync(async db =>
            await db.SessionVideos.Where(v => v.SessionId == ctx.GatedSessionId).Select(v => v.Id).FirstAsync());
        await factory.SeedReadyHlsAsync(videoId);

        var response = await ctx.Student.PostAsync($"/api/me/videos/{videoId}/playback", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReasonAsync(response)).Should().Be("quiz_required");
    }

    [Fact]
    public async Task Gate_enforces_default_deny_and_tenant_isolation()
    {
        var ctx = await SetupPlayableAsync();

        // Anonymous → 401; staff → 403 (RequireStudent).
        var anon = factory.CreateClient();
        (await anon.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync($"/api/me/videos/{ctx.VideoId}/hls.key"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var staff = factory.CreateClientFor(StaffRole.Teacher, ctx.Tenant);
        (await staff.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync($"/api/me/videos/{ctx.VideoId}/hls.key"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A student in another tenant cannot resolve the video → 404 (not a leak).
        var otherTenant = await factory.SeedTenantAsync();
        var otherStudent = factory.CreateClientForStudent(otherTenant, Guid.NewGuid());
        (await otherStudent.PostAsync($"/api/me/videos/{ctx.VideoId}/playback", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
