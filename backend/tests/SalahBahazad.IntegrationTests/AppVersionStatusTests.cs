using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Features.App.DTOs;
using SalahBahazad.Application.Features.Videos.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// <c>GET /api/app/version-status</c> and <c>POST /api/me/videos/playback/redeem</c> min-version enforcement
/// (contract §F, FR-APP-UPD-001, NFR-APP-UPD-002). Proves: the three status values across platforms;
/// bad-request guards; the <c>426 outdated_app</c> gate at redeem; leniency for absent headers; the
/// portal gate (<c>StartVideoPlayback</c>) is unaffected by version headers; and the version floor is
/// per-platform.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AppVersionStatusTests(SalahBahazadApiFactory factory)
{
    private sealed record VersionStatusMirror(string Status, string MinVersion, string LatestVersion, string StoreUrl);
    private sealed record ProblemReasonMirror(string? Reason);

    // ── Anonymous client (version-status is AllowAnonymous) ─────────────────────

    private HttpClient Anon() => factory.CreateClient();

    // ── GET /api/app/version-status ─────────────────────────────────────────────

    [Fact]
    public async Task VersionStatus_returns_ok_when_version_matches_floor()
    {
        // Default appsettings floor is 1.0.0 / latest 1.0.0 — calling with 1.0.0 → ok.
        var resp = await Anon().GetAsync("/api/app/version-status?platform=android&version=1.0.0");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<VersionStatusMirror>(TestJson.Options);
        body!.Status.Should().Be("ok");
        body.MinVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task VersionStatus_returns_ok_when_version_exceeds_latest()
    {
        var resp = await Anon().GetAsync("/api/app/version-status?platform=ios&version=9.9.9");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<VersionStatusMirror>(TestJson.Options);
        body!.Status.Should().Be("ok");
    }

    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    [InlineData("windows")]
    [InlineData("macos")]
    public async Task VersionStatus_resolves_all_four_platforms(string platform)
    {
        var resp = await Anon().GetAsync($"/api/app/version-status?platform={platform}&version=1.0.0");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VersionStatus_returns_update_required_when_below_floor()
    {
        // Override the floor to 99.0.0 for this request via a derived factory.
        await using var elevated = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("AppVersions:Platforms:android:MinVersion", "99.0.0");
            b.UseSetting("AppVersions:Platforms:android:LatestVersion", "99.0.0");
            b.UseSetting("AppVersions:Platforms:android:StoreUrl", "https://play.google.com/test");
        });
        var client = elevated.CreateClient();

        var resp = await client.GetAsync("/api/app/version-status?platform=android&version=1.0.0");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<VersionStatusMirror>(TestJson.Options);
        body!.Status.Should().Be("update_required");
        body.MinVersion.Should().Be("99.0.0");
        body.StoreUrl.Should().Be("https://play.google.com/test");
    }

    [Fact]
    public async Task VersionStatus_returns_update_available_when_below_latest_but_at_or_above_floor()
    {
        await using var elevated = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("AppVersions:Platforms:android:MinVersion", "1.0.0");
            b.UseSetting("AppVersions:Platforms:android:LatestVersion", "9.0.0");
        });
        var client = elevated.CreateClient();

        var resp = await client.GetAsync("/api/app/version-status?platform=android&version=1.0.0");
        var body = await resp.Content.ReadFromJsonAsync<VersionStatusMirror>(TestJson.Options);
        body!.Status.Should().Be("update_available");
        body.LatestVersion.Should().Be("9.0.0");
    }

    [Fact]
    public async Task VersionStatus_returns_400_for_unknown_platform()
    {
        var resp = await Anon().GetAsync("/api/app/version-status?platform=fridge&version=1.0.0");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VersionStatus_returns_400_for_missing_version()
    {
        var resp = await Anon().GetAsync("/api/app/version-status?platform=android");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VersionStatus_returns_400_for_unparseable_version()
    {
        var resp = await Anon().GetAsync("/api/app/version-status?platform=android&version=not-a-version");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VersionStatus_returns_400_for_missing_platform()
    {
        var resp = await Anon().GetAsync("/api/app/version-status?version=1.0.0");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VersionStatus_requires_no_auth()
    {
        // No Authorization header — the endpoint is AllowAnonymous.
        var resp = await Anon().GetAsync("/api/app/version-status?platform=android&version=1.0.0");
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    // ── Redeem version enforcement (contract §F.2) ───────────────────────────────

    private async Task<(Guid tenantId, Guid studentId, Guid videoId, Guid enrollmentId)> SetupPlayableAsync(
        HttpClient elevatedClient)
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, videoCount: 1, accessPerVideo: 2);
        var videoId = await factory.QueryDbAsync(async db =>
            await db.SessionVideos.Where(v => v.SessionId == session.Id).Select(v => v.Id)
                .FirstAsync());

        await factory.SeedReadyHlsAsync(videoId);

        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var teacherClient = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacherClient.GenerateOneSerialAsync(session.Id);
        var enrollment = await studentClient.RedeemAsync(serial);

        return (tenant, student.Id, videoId, enrollment.Id);
    }

    [Fact]
    public async Task Redeem_returns_426_when_app_version_is_below_floor()
    {
        await using var elevated = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("AppVersions:Platforms:windows:MinVersion", "99.0.0");
            b.UseSetting("AppVersions:Platforms:windows:LatestVersion", "99.0.0");
            b.UseSetting("AppVersions:Platforms:windows:StoreUrl", "https://example.com/update");
        });

        var (tenant, studentId, videoId, _) = await SetupPlayableAsync(elevated.CreateClient());
        var studentClient = elevated.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", factory.CreateStudentToken(tenant, studentId));

        // Mint a handoff (gate is unaffected by version headers).
        var gate = await studentClient.PostAsync($"/api/me/videos/{videoId}/playback", null);
        gate.StatusCode.Should().Be(HttpStatusCode.OK, "the gate must succeed regardless of version headers");
        var handoff = (await gate.Content.ReadFromJsonAsync<PlaybackHandoffDto>(TestJson.Options))!;
        var handoffCode = handoff.HandoffCode;

        // Redeem with stale version → 426.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/videos/playback/redeem")
        {
            Content = JsonContent.Create(new { handoffCode }),
        };
        request.Headers.Add("X-App-Version", "1.0.0");
        request.Headers.Add("X-Platform", "windows");

        var redeem = await studentClient.SendAsync(request);
        redeem.StatusCode.Should().Be(HttpStatusCode.UpgradeRequired, "stale app version must be rejected");

        var body = await redeem.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        body!.Reason.Should().Be("outdated_app");
    }

    [Fact]
    public async Task Redeem_at_exact_floor_version_succeeds()
    {
        // Default appsettings floor is 1.0.0 — sending exactly 1.0.0 must not trigger 426.
        var (tenant, studentId, videoId, _) = await SetupPlayableAsync(factory.CreateClient());
        var studentClient = factory.CreateClientForStudent(tenant, studentId);

        var gate = await studentClient.PostAsync($"/api/me/videos/{videoId}/playback", null);
        var handoff = (await gate.Content.ReadFromJsonAsync<PlaybackHandoffDto>(TestJson.Options))!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/videos/playback/redeem")
        {
            Content = JsonContent.Create(new { handoffCode = handoff.HandoffCode }),
        };
        request.Headers.Add("X-App-Version", "1.0.0");
        request.Headers.Add("X-Platform", "android");

        var redeem = await studentClient.SendAsync(request);
        redeem.StatusCode.Should().Be(HttpStatusCode.OK, "version at the floor must not be blocked");
    }

    [Fact]
    public async Task Redeem_without_version_headers_succeeds_for_portal_compatibility()
    {
        // The browser portal never sends X-App-Version / X-Platform — leniency rule (contract §F.2).
        await using var elevated = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("AppVersions:Platforms:android:MinVersion", "99.0.0");
        });

        var (tenant, studentId, videoId, _) = await SetupPlayableAsync(elevated.CreateClient());
        var studentClient = elevated.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", factory.CreateStudentToken(tenant, studentId));

        var gate = await studentClient.PostAsync($"/api/me/videos/{videoId}/playback", null);
        var handoff = (await gate.Content.ReadFromJsonAsync<PlaybackHandoffDto>(TestJson.Options))!;

        // No version headers at all.
        var redeem = await studentClient.PostAsJsonAsync(
            "/api/me/videos/playback/redeem",
            new { handoffCode = handoff.HandoffCode },
            TestJson.Options);

        redeem.StatusCode.Should().Be(HttpStatusCode.OK, "absent headers must not block the portal path");
    }

    [Fact]
    public async Task Gate_StartVideoPlayback_is_not_affected_by_version_headers()
    {
        // The gate is called by the portal (not the app). Even with a 99.0.0 floor and a stale version
        // header on the GATE call, it must succeed — the version check only fires at redeem.
        await using var elevated = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("AppVersions:Platforms:android:MinVersion", "99.0.0");
        });

        var (tenant, studentId, videoId, _) = await SetupPlayableAsync(elevated.CreateClient());
        var studentClient = elevated.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", factory.CreateStudentToken(tenant, studentId));

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/me/videos/{videoId}/playback");
        request.Headers.Add("X-App-Version", "0.0.1");
        request.Headers.Add("X-Platform", "android");

        var gate = await studentClient.SendAsync(request);
        gate.StatusCode.Should().Be(HttpStatusCode.OK, "the gate must not enforce the version floor");
    }

    [Fact]
    public async Task Redeem_426_does_not_consume_the_handoff()
    {
        // After a 426, redeeming the same handoff with a compliant version must succeed (no double-spend).
        await using var elevated = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("AppVersions:Platforms:android:MinVersion", "2.0.0");
            b.UseSetting("AppVersions:Platforms:android:LatestVersion", "2.0.0");
        });

        var (tenant, studentId, videoId, _) = await SetupPlayableAsync(elevated.CreateClient());
        var studentClient = elevated.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", factory.CreateStudentToken(tenant, studentId));

        var gate = await studentClient.PostAsync($"/api/me/videos/{videoId}/playback", null);
        var handoff = (await gate.Content.ReadFromJsonAsync<PlaybackHandoffDto>(TestJson.Options))!;
        var handoffCode = handoff.HandoffCode;

        // Attempt with stale version → 426 (should not consume the handoff).
        var stale = new HttpRequestMessage(HttpMethod.Post, "/api/me/videos/playback/redeem")
        {
            Content = JsonContent.Create(new { handoffCode }),
        };
        stale.Headers.Add("X-App-Version", "1.0.0");
        stale.Headers.Add("X-Platform", "android");
        (await studentClient.SendAsync(stale)).StatusCode.Should().Be(HttpStatusCode.UpgradeRequired);

        // Now try with a compliant version — the handoff must still be valid.
        var compliant = new HttpRequestMessage(HttpMethod.Post, "/api/me/videos/playback/redeem")
        {
            Content = JsonContent.Create(new { handoffCode }),
        };
        compliant.Headers.Add("X-App-Version", "2.0.0");
        compliant.Headers.Add("X-Platform", "android");
        (await studentClient.SendAsync(compliant)).StatusCode.Should().Be(HttpStatusCode.OK,
            "the handoff must still be valid after a 426 rejection");
    }
}
