using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Student sign-in exchange + device binding end-to-end through the real stack (Testcontainers, faked
/// Firebase): status gate, one-device enforcement, the HttpOnly device cookie, Student-role JWT claims,
/// role-aware refresh, default-deny + tenant isolation, and the "everything is audited" rows (§1.5).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StudentAuthTests(SalahBahazadApiFactory factory)
{
    // ── Happy path ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Exchange_signs_in_active_student_sets_device_cookie_issues_student_token_and_audits()
    {
        var (student, tenantId, token) = await SeedSignedInStudentAsync();

        var client = AnonClient();
        var response = await ExchangeAsync(client, token, fingerprint: "Android · Chrome");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);
        body!.Student.Id.Should().Be(student.Id);
        body.Student.Status.Should().Be("Active");
        body.Student.BoundDevice!.Summary.Should().Be("Android · Chrome");

        // HttpOnly device cookie is issued on the bind (§1.3).
        var (cookieValue, rawCookie) = ExtractDeviceCookie(response);
        cookieValue.Should().NotBeNullOrWhiteSpace();
        rawCookie.ToLowerInvariant().Should().Contain("httponly");

        // The access token is a Student-role token carrying tenant_id + device_id (§1.2 / Step 4).
        Claim(body.AccessToken, "role", ClaimTypes.Role).Should().Be("Student");
        Claim(body.AccessToken, "tenant_id").Should().Be(tenantId.ToString());
        Guid.TryParse(Claim(body.AccessToken, "device_id"), out _).Should().BeTrue();

        // A single active device row exists, with the fingerprint persisted for staff visibility.
        var device = await factory.QueryDbAsync(db => db.StudentDevices
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(d => d.StudentId == student.Id && d.IsActive));
        device.Should().NotBeNull();
        device!.FingerprintSummary.Should().Be("Android · Chrome");

        // Audited: StudentSignedIn (Student actor, student portal) + StudentDeviceBound on the new bind.
        var signedIn = await factory.LatestAuditAsync(tenantId, "Student", "StudentSignedIn");
        signedIn.Should().NotBeNull();
        signedIn!.ActorType.Should().Be("Student");
        signedIn.Portal.Should().Be("student");
        (await factory.LatestAuditAsync(tenantId, "StudentDevice", "StudentDeviceBound")).Should().NotBeNull();
    }

    // ── Status gate (FR-PLAT-AUTH-005) ─────────────────────────────────────────────

    [Theory]
    [InlineData(StudentStatus.Pending, "account_pending")]
    [InlineData(StudentStatus.Inactive, "account_inactive")]
    public async Task Blocked_status_is_403_with_reason_and_writes_a_rejection_audit(
        StudentStatus status, string expectedReason)
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenant);
        var student = await factory.SeedStudentAsync(tenant, grade.Id, cityId, regionId, status);
        var token = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(token, student.FirebaseUid);

        var response = await ExchangeAsync(AnonClient(), token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be(expectedReason);

        var rejected = await factory.LatestAuditAsync(tenant, "Student", "StudentSignInRejected");
        rejected.Should().NotBeNull();
        rejected!.ActorType.Should().Be("Student");
        rejected.Summary.Should().Contain(expectedReason);
    }

    [Fact]
    public async Task Rejected_status_echoes_the_stored_rejection_reason_and_audits()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenant);
        var student = await factory.SeedStudentAsync(
            tenant, grade.Id, cityId, regionId, StudentStatus.Rejected); // SeedStudentAsync rejects with "seeded rejection"
        var token = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(token, student.FirebaseUid);

        var response = await ExchangeAsync(AnonClient(), token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be("account_rejected");
        problem.Detail.Should().Be("seeded rejection");
        (await factory.LatestAuditAsync(tenant, "Student", "StudentSignInRejected")).Should().NotBeNull();
    }

    // ── One-device enforcement (FR-PLAT-DEV-001/003) ───────────────────────────────

    [Fact]
    public async Task Second_device_is_blocked_then_the_recognised_cookie_is_allowed()
    {
        var (student, tenantId, token) = await SeedSignedInStudentAsync();
        var client = AnonClient();

        // First sign-in binds and sets the cookie.
        var first = await ExchangeAsync(client, token, fingerprint: "iOS 18 · Safari");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var (cookie, _) = ExtractDeviceCookie(first);

        // A different device (no cookie) is rejected and the attempt is audited.
        var second = await ExchangeAsync(client, token, fingerprint: "Android · Chrome");
        second.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await second.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be("device_not_recognized");

        // The original device (its cookie) is recognised → allowed, with no second bind.
        var third = await ExchangeAsync(client, token, cookie, fingerprint: "iOS 18 · Safari");
        third.StatusCode.Should().Be(HttpStatusCode.OK);

        (await factory.CountAuditAsync(tenantId, "StudentDevice", "StudentDeviceBound")).Should().Be(1);
        (await factory.CountAuditAsync(tenantId, "Student", "StudentSignInRejected")).Should().Be(1);
    }

    // ── Role-aware refresh (FR-PLAT-AUTH-002, reused endpoint) ──────────────────────

    [Fact]
    public async Task Student_refresh_reissues_a_student_pair_preserving_device_then_401_after_deactivate()
    {
        var (student, tenantId, token) = await SeedSignedInStudentAsync();
        var client = AnonClient();

        var signIn = await ExchangeAsync(client, token, fingerprint: "iOS 18 · Safari");
        signIn.StatusCode.Should().Be(HttpStatusCode.OK);
        var signInBody = await signIn.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);
        var originalDeviceId = Claim(signInBody!.AccessToken, "device_id");

        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = signInBody.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refresh.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);
        refreshed!.Student.Id.Should().Be(student.Id);
        Claim(refreshed.AccessToken, "role", ClaimTypes.Role).Should().Be("Student");
        Claim(refreshed.AccessToken, "device_id").Should().Be(originalDeviceId); // device preserved

        // Deactivate the student → its refresh token can no longer refresh (reload from DB, not the token).
        await DeactivateStudentAsync(student.Id);
        var afterDeactivate = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = refreshed.RefreshToken });
        afterDeactivate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Default-deny + tenant isolation (NFR-SEC-010) ──────────────────────────────

    [Fact]
    public async Task A_staff_firebase_account_and_an_unknown_account_are_401_on_student_exchange()
    {
        var tenant = await factory.SeedTenantAsync();
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var staffToken = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(staffToken, staff.FirebaseUid); // a staff UID has no Student row

        (await ExchangeAsync(AnonClient(), staffToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // An unpinned token → the fake returns a fresh UID with no account at all.
        (await ExchangeAsync(AnonClient(), $"unknown-{Guid.NewGuid():N}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Exchange_returns_only_the_callers_own_tenant_and_identity()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();
        var gradeA = await factory.SeedGradeAsync(tenantA);
        var studentA = await factory.SeedStudentAsync(tenantA, gradeA.Id, cityId, regionId, StudentStatus.Active);
        var tokenA = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(tokenA, studentA.FirebaseUid);

        var response = await ExchangeAsync(AnonClient(), tokenA, fingerprint: "iOS 18 · Safari");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);

        body!.Student.Id.Should().Be(studentA.Id);
        Claim(body.AccessToken, "tenant_id").Should().Be(tenantA.ToString());
        Claim(body.AccessToken, "tenant_id").Should().NotBe(tenantB.ToString());
    }

    [Fact]
    public async Task Staff_exchange_is_unaffected_by_the_student_surface()
    {
        var tenant = await factory.SeedTenantAsync();
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var token = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(token, staff.FirebaseUid);

        var response = await AnonClient().PostAsJsonAsync(
            "/api/auth/exchange", new { firebaseIdToken = token });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthTokenMirror>(TestJson.Options);
        Claim(body!.AccessToken, "role", ClaimTypes.Role).Should().Be("Teacher");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private async Task<(Domain.Entities.Student Student, Guid TenantId, string IdToken)> SeedSignedInStudentAsync()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenant);
        var student = await factory.SeedStudentAsync(tenant, grade.Id, cityId, regionId, StudentStatus.Active);
        var token = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(token, student.FirebaseUid);
        return (student, tenant, token);
    }

    private async Task DeactivateStudentAsync(Guid studentId) =>
        await factory.QueryDbAsync(async db =>
        {
            var student = await db.Students.IgnoreQueryFilters().FirstAsync(s => s.Id == studentId);
            student.Deactivate();
            await db.SaveChangesAsync();
            return true;
        });

    /// <summary>A client that does NOT auto-manage cookies, so the device cookie is asserted/sent explicitly.</summary>
    private HttpClient AnonClient() =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private static async Task<HttpResponseMessage> ExchangeAsync(
        HttpClient client, string idToken, string? deviceCookie = null, string? fingerprint = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/student/exchange")
        {
            Content = JsonContent.Create(new { firebaseIdToken = idToken }),
        };
        if (deviceCookie is not null)
            request.Headers.Add("Cookie", $"sb_device={deviceCookie}");
        if (fingerprint is not null)
            request.Headers.Add("X-Device-Fingerprint", fingerprint);
        return await client.SendAsync(request);
    }

    private static (string Value, string Raw) ExtractDeviceCookie(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue("a device cookie is set on bind");
        var raw = cookies!.Single(c => c.StartsWith("sb_device=", StringComparison.Ordinal));
        var value = raw["sb_device=".Length..];
        var semicolon = value.IndexOf(';');
        if (semicolon >= 0)
            value = value[..semicolon];
        return (value, raw);
    }

    private static string? Claim(string jwt, params string[] types)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        return token.Claims.FirstOrDefault(c => types.Contains(c.Type))?.Value;
    }
}
