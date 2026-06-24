using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The device-agnostic native-app sign-in (<c>POST /api/auth/student/app-exchange</c>) and the app-aware
/// refresh branch, end-to-end through the real stack (Testcontainers, faked Firebase). Proves contract §A/§B:
/// the same status gate as the portal exchange, but NO device binding — app JWTs carry no <c>device_id</c>,
/// a portal-bound student still signs into the app from any machine, two app sign-ins write no binding rows,
/// an app refresh works with no <c>StudentDevice</c>, and the portal refresh device re-check is unaffected.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StudentAppAuthTests(SalahBahazadApiFactory factory)
{
    // ── Happy path (contract §A.1, §I) ─────────────────────────────────────────────

    [Fact]
    public async Task App_exchange_signs_in_active_student_with_no_device_id_no_cookie_and_audits()
    {
        var (student, tenantId, token) = await SeedSignedInStudentAsync();

        var response = await AppExchangeAsync(AnonClient(), token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);
        body!.Student.Id.Should().Be(student.Id);
        body.Student.Status.Should().Be("Active");
        body.Student.BoundDevice.Should().BeNull("the app session binds no device (contract §A.1)");

        // The access token is a Student-role token with tenant_id but NO device_id (device-agnostic).
        Claim(body.AccessToken, "role", ClaimTypes.Role).Should().Be("Student");
        Claim(body.AccessToken, "tenant_id").Should().Be(tenantId.ToString());
        Claim(body.AccessToken, "device_id").Should().BeNull();
        Claim(body.RefreshToken, "device_id").Should().BeNull();

        // No device cookie is set, and no device row is written.
        HasDeviceCookie(response).Should().BeFalse("the app never sets the sb_device cookie");
        (await ActiveDeviceCountAsync(student.Id)).Should().Be(0);

        // Audited: exactly one StudentSignedIn (Student actor, "app" portal) and NO StudentDeviceBound (§I).
        var signedIn = await factory.LatestAuditAsync(tenantId, "Student", "StudentSignedIn");
        signedIn.Should().NotBeNull();
        signedIn!.ActorType.Should().Be("Student");
        signedIn.Portal.Should().Be("app");
        (await factory.CountAuditAsync(tenantId, "Student", "StudentSignedIn")).Should().Be(1);
        (await factory.CountAuditAsync(tenantId, "StudentDevice", "StudentDeviceBound")).Should().Be(0);
    }

    // ── Device-agnostic headline (FR-APP-DEV-001) ──────────────────────────────────

    [Fact]
    public async Task A_portal_bound_student_still_signs_into_the_app_from_another_device()
    {
        var (student, _, token) = await SeedSignedInStudentAsync();
        var client = AnonClient();

        // Bind a device through the PORTAL exchange (issues the sb_device cookie + a StudentDevice row).
        var portalBind = await PortalExchangeAsync(client, token, fingerprint: "iOS 18 · Safari");
        portalBind.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ActiveDeviceCountAsync(student.Id)).Should().Be(1);

        // Contrast: a second PORTAL exchange from a different device (no cookie) is rejected.
        var portalSecondDevice = await PortalExchangeAsync(client, token, fingerprint: "Android · Chrome");
        portalSecondDevice.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await portalSecondDevice.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be("device_not_recognized");

        // The APP exchange from that same other device (no cookie) is allowed — device-agnostic.
        var appResponse = await AppExchangeAsync(client, token);
        appResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await appResponse.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);
        body!.Student.BoundDevice.Should().BeNull();
        Claim(body.AccessToken, "device_id").Should().BeNull();

        // The pre-existing portal binding is untouched (still exactly one active device, no new binds).
        (await ActiveDeviceCountAsync(student.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Two_app_exchanges_from_different_devices_both_succeed_and_write_no_binding_rows()
    {
        var (student, _, token) = await SeedSignedInStudentAsync();

        // No cookie either time (two distinct "devices"): both succeed, no binding is ever written.
        (await AppExchangeAsync(AnonClient(), token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AppExchangeAsync(AnonClient(), token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ActiveDeviceCountAsync(student.Id)).Should().Be(0);
    }

    // ── Status gate (FR-PLAT-AUTH-005), shared with the portal path ────────────────

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

        var response = await AppExchangeAsync(AnonClient(), token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be(expectedReason);

        var rejected = await factory.LatestAuditAsync(tenant, "Student", "StudentSignInRejected");
        rejected.Should().NotBeNull();
        rejected!.ActorType.Should().Be("Student");
        rejected.Portal.Should().Be("app");
        rejected.Summary.Should().Contain(expectedReason);
    }

    [Fact]
    public async Task Rejected_status_echoes_the_stored_rejection_reason()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenant);
        var student = await factory.SeedStudentAsync(
            tenant, grade.Id, cityId, regionId, StudentStatus.Rejected); // rejects with "seeded rejection"
        var token = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(token, student.FirebaseUid);

        var response = await AppExchangeAsync(AnonClient(), token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be("account_rejected");
        problem.Detail.Should().Be("seeded rejection");
    }

    // ── Default-deny + validation ──────────────────────────────────────────────────

    [Fact]
    public async Task A_staff_account_and_an_unknown_account_are_401()
    {
        var tenant = await factory.SeedTenantAsync();
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var staffToken = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(staffToken, staff.FirebaseUid); // a staff UID has no Student row

        (await AppExchangeAsync(AnonClient(), staffToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // An unpinned token → the fake returns a fresh UID with no account at all.
        (await AppExchangeAsync(AnonClient(), $"unknown-{Guid.NewGuid():N}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Empty_firebase_token_is_400()
    {
        var response = await AppExchangeAsync(AnonClient(), idToken: "");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task App_exchange_returns_only_the_callers_own_tenant()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();
        var gradeA = await factory.SeedGradeAsync(tenantA);
        var studentA = await factory.SeedStudentAsync(tenantA, gradeA.Id, cityId, regionId, StudentStatus.Active);
        var tokenA = $"tok-{Guid.NewGuid():N}";
        factory.PinFirebaseUser(tokenA, studentA.FirebaseUid);

        var response = await AppExchangeAsync(AnonClient(), tokenA);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);

        body!.Student.Id.Should().Be(studentA.Id);
        Claim(body.AccessToken, "tenant_id").Should().Be(tenantA.ToString());
        Claim(body.AccessToken, "tenant_id").Should().NotBe(tenantB.ToString());
    }

    // ── App-aware refresh (contract §B) ────────────────────────────────────────────

    [Fact]
    public async Task App_refresh_token_without_a_device_refreshes_to_200_and_stays_device_less()
    {
        var (student, _, token) = await SeedSignedInStudentAsync();
        var client = AnonClient();

        var signIn = await AppExchangeAsync(client, token);
        signIn.StatusCode.Should().Be(HttpStatusCode.OK);
        var signInBody = await signIn.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);

        // No StudentDevice exists for this student — the app path never bound one.
        (await ActiveDeviceCountAsync(student.Id)).Should().Be(0);

        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = signInBody!.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refresh.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);
        refreshed!.Student.Id.Should().Be(student.Id);
        refreshed.Student.BoundDevice.Should().BeNull();
        Claim(refreshed.AccessToken, "role", ClaimTypes.Role).Should().Be("Student");
        Claim(refreshed.AccessToken, "device_id").Should().BeNull("the refreshed app token stays device-agnostic");
        Claim(refreshed.RefreshToken, "device_id").Should().BeNull();
    }

    [Fact]
    public async Task App_refresh_is_401_after_the_student_is_deactivated()
    {
        var (student, _, token) = await SeedSignedInStudentAsync();
        var client = AnonClient();

        var signIn = await AppExchangeAsync(client, token);
        var signInBody = await signIn.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);

        await DeactivateStudentAsync(student.Id);

        var afterDeactivate = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = signInBody!.RefreshToken });
        afterDeactivate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Portal_refresh_still_requires_an_active_device_after_a_clear()
    {
        // Regression: the portal (device_id-bearing) refresh path is unchanged — a cleared device → 401.
        var (student, _, token) = await SeedSignedInStudentAsync();
        var client = AnonClient();

        var portalSignIn = await PortalExchangeAsync(client, token, fingerprint: "iOS 18 · Safari");
        portalSignIn.StatusCode.Should().Be(HttpStatusCode.OK);
        var portalBody = await portalSignIn.Content.ReadFromJsonAsync<StudentAuthMirror>(TestJson.Options);
        Claim(portalBody!.AccessToken, "device_id").Should().NotBeNull("the portal token is device-bound");

        // Staff clears the device → the bound portal refresh token can no longer refresh.
        await DeactivateDevicesAsync(student.Id);

        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = portalBody.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private Task<int> ActiveDeviceCountAsync(Guid studentId) =>
        factory.QueryDbAsync(db => db.StudentDevices
            .IgnoreQueryFilters()
            .CountAsync(d => d.StudentId == studentId && d.IsActive));

    private async Task DeactivateStudentAsync(Guid studentId) =>
        await factory.QueryDbAsync(async db =>
        {
            var student = await db.Students.IgnoreQueryFilters().FirstAsync(s => s.Id == studentId);
            student.Deactivate();
            await db.SaveChangesAsync();
            return true;
        });

    private async Task DeactivateDevicesAsync(Guid studentId) =>
        await factory.QueryDbAsync(async db =>
        {
            await db.StudentDevices
                .IgnoreQueryFilters()
                .Where(d => d.StudentId == studentId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false));
            return true;
        });

    /// <summary>A client that does NOT auto-manage cookies, so the (absent) device cookie is asserted explicitly.</summary>
    private HttpClient AnonClient() =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private static Task<HttpResponseMessage> AppExchangeAsync(HttpClient client, string idToken) =>
        client.PostAsync(
            "/api/auth/student/app-exchange",
            JsonContent.Create(new { firebaseIdToken = idToken }));

    private static async Task<HttpResponseMessage> PortalExchangeAsync(
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

    private static bool HasDeviceCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("sb_device=", StringComparison.Ordinal));

    private static string? Claim(string jwt, params string[] types)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        return token.Claims.FirstOrDefault(c => types.Contains(c.Type))?.Value;
    }
}
