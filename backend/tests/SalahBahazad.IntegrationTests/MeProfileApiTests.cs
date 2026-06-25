using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The student-portal S6 self-service profile (contract §A/§B/§C/§E, FR-STU-PRO-001/002, FR-STU-DEV-003): the GET
/// read shape incl. resolved grade/city/region names + the active bound device (and null when none); the PUT happy
/// path persisting all seven writable fields + the audited "Updated Student" row (ActorType=Student); grade + email
/// are not writable / not returned; the device token hash is never leaked; 400 on bad shape or unknown/mismatched
/// city/region; the read is not audited; 401 anon / 403 staff; and cross-tenant isolation (NFR-SEC-010). There is no
/// URL id, so IDOR is not applicable — the caller is always the JWT subject.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MeProfileApiTests(SalahBahazadApiFactory factory)
{
    private async Task<(Guid Tenant, Guid GradeId, Guid CityId, Guid RegionId, StudentEntity Student)> SetupAsync(
        StudentStatus status = StudentStatus.Active)
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, _) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, status);
        return (tenant, gradeId, cityId, regionId, student);
    }

    private Task<StudentProfileResponse> GetProfileAsync(HttpClient client)
        => client.GetFromJsonAsync<StudentProfileResponse>("/api/me/profile", TestJson.Options)!;

    /// <summary>A second valid (city, region) pair distinct from <paramref name="cityId"/> so a PUT can move both.</summary>
    private async Task<(Guid CityId, Guid RegionId)> OtherLocationAsync(Guid cityId)
    {
        var regionId = await factory.QueryDbAsync(db => db.Regions
            .Where(r => r.CityId != cityId).Select(r => r.Id).FirstAsync());
        var otherCityId = await factory.QueryDbAsync(db => db.Regions
            .Where(r => r.Id == regionId).Select(r => r.CityId).FirstAsync());
        return (otherCityId, regionId);
    }

    private static UpdateMyStudentProfileBody ValidBody(Guid cityId, Guid regionId) => new(
        FullName: "Updated Name",
        PhoneNumber: "01099999999",
        SchoolName: "New School",
        CityId: cityId,
        RegionId: regionId,
        ParentPhonePrimary: "01088888888",
        ParentPhoneSecondary: "01077777777");

    private Task<int> TotalAuditAsync(Guid tenantId)
        => factory.QueryDbAsync(db => db.AuditEntries.CountAsync(a => a.TenantId == tenantId));

    // ── §A.1 GET shape ───────────────────────────────────────────────────────
    [Fact]
    public async Task Get_returns_the_callers_profile_with_resolved_names_and_bound_device()
    {
        var (tenant, gradeId, cityId, regionId, student) = await SetupAsync();
        var device = await factory.SeedDeviceAsync(tenant, student.Id);

        var profile = await GetProfileAsync(factory.CreateClientForStudent(tenant, student.Id));

        profile.Id.Should().Be(student.Id);
        profile.Serial.Should().Match("STU-*");                  // watermark identity (FR-APP-VID-003)
        profile.FullName.Should().Be("Seed Student");
        profile.PhoneNumber.Should().Be("01055555555");
        profile.ParentPhonePrimary.Should().Be("01000000000");
        profile.ParentPhoneSecondary.Should().BeNull();
        profile.SchoolName.Should().Be("Seed School");
        profile.GradeId.Should().Be(gradeId);
        profile.GradeName.Should().NotBeNullOrWhiteSpace();
        profile.CityId.Should().Be(cityId);
        profile.CityName.Should().NotBeNullOrWhiteSpace();
        profile.RegionId.Should().Be(regionId);
        profile.RegionName.Should().NotBeNullOrWhiteSpace();
        profile.Status.Should().Be("Active");

        profile.BoundDevice.Should().NotBeNull();
        profile.BoundDevice!.Summary.Should().Be("iOS 18 · Safari");
        profile.BoundDevice.BoundAtUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-5));

        // The token hash is never exposed (§C.5); neither is email or avatar (§C.2/§F).
        var raw = await factory.CreateClientForStudent(tenant, student.Id).GetStringAsync("/api/me/profile");
        var tokenHash = await factory.QueryDbAsync(db => db.StudentDevices
            .IgnoreQueryFilters()           // no HTTP context here → tenant resolves to Empty (factory docstring)
            .Where(d => d.Id == device.Id).Select(d => d.DeviceTokenHash).FirstAsync());
        raw.Should().Contain("\"serial\"");
        raw.Should().NotContain(tokenHash);
        raw.Should().NotContain("deviceTokenHash").And.NotContain("tokenHash");
        raw.Should().NotContain("\"email\"").And.NotContain("\"avatar\"");
    }

    [Fact]
    public async Task Get_bound_device_is_null_when_no_active_device()
    {
        var (tenant, _, _, _, student) = await SetupAsync();

        var profile = await GetProfileAsync(factory.CreateClientForStudent(tenant, student.Id));

        profile.BoundDevice.Should().BeNull();
    }

    [Fact]
    public async Task Get_is_not_audited()
    {
        var (tenant, _, _, _, student) = await SetupAsync();
        var before = await TotalAuditAsync(tenant);

        await GetProfileAsync(factory.CreateClientForStudent(tenant, student.Id));
        await GetProfileAsync(factory.CreateClientForStudent(tenant, student.Id));

        (await TotalAuditAsync(tenant)).Should().Be(before);   // pure read (§E)
    }

    // ── §A.2 PUT happy path + audit ──────────────────────────────────────────
    [Fact]
    public async Task Put_persists_all_seven_writable_fields_and_writes_a_student_audit_row()
    {
        var (tenant, gradeId, cityId, _, student) = await SetupAsync();
        var (city2, region2) = await OtherLocationAsync(cityId);
        var client = factory.CreateClientForStudent(tenant, student.Id);

        var response = await client.PutAsJsonAsync("/api/me/profile", ValidBody(city2, region2), TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<StudentProfileResponse>(TestJson.Options))!;

        // All seven writable fields applied; grade + status unchanged.
        updated.FullName.Should().Be("Updated Name");
        updated.PhoneNumber.Should().Be("01099999999");
        updated.SchoolName.Should().Be("New School");
        updated.CityId.Should().Be(city2);
        updated.RegionId.Should().Be(region2);
        updated.ParentPhonePrimary.Should().Be("01088888888");
        updated.ParentPhoneSecondary.Should().Be("01077777777");
        updated.GradeId.Should().Be(gradeId);            // grade not changed (§C.1)
        updated.Status.Should().Be("Active");

        // Persisted: a fresh GET (new client → fresh DbContext → real DB round-trip) returns ALL seven.
        var reread = await GetProfileAsync(factory.CreateClientForStudent(tenant, student.Id));
        reread.FullName.Should().Be("Updated Name");
        reread.PhoneNumber.Should().Be("01099999999");
        reread.SchoolName.Should().Be("New School");
        reread.CityId.Should().Be(city2);
        reread.RegionId.Should().Be(region2);
        reread.ParentPhonePrimary.Should().Be("01088888888");
        reread.ParentPhoneSecondary.Should().Be("01077777777");

        // Audited automatically by the interceptor as exactly one "Updated Student" row, attributed to the student.
        (await factory.CountAuditAsync(tenant, "Student", "Updated")).Should().Be(1);
        var audit = await factory.LatestAuditAsync(tenant, "Student", "Updated");
        audit.Should().NotBeNull();
        audit!.ActorType.Should().Be("Student");
        audit.ActorId.Should().Be(student.Id);
    }

    [Fact]
    public async Task Put_clears_optional_secondary_phone_when_omitted()
    {
        var (tenant, _, cityId, regionId, student) = await SetupAsync();
        var client = factory.CreateClientForStudent(tenant, student.Id);

        var body = ValidBody(cityId, regionId) with { ParentPhoneSecondary = null };
        var response = await client.PutAsJsonAsync("/api/me/profile", body, TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<StudentProfileResponse>(TestJson.Options))!;
        updated.ParentPhoneSecondary.Should().BeNull();
    }

    // ── §C.1/§C.2 grade + email are not writable / not returned ──────────────
    [Fact]
    public async Task Put_ignores_grade_and_email_in_the_body()
    {
        var (tenant, gradeId, cityId, regionId, student) = await SetupAsync();
        var client = factory.CreateClientForStudent(tenant, student.Id);

        // A payload that smuggles a different gradeId + an email alongside the valid writable fields.
        var smuggled = new
        {
            fullName = "Updated Name",
            phoneNumber = "01099999999",
            schoolName = "New School",
            cityId,
            regionId,
            parentPhonePrimary = "01088888888",
            parentPhoneSecondary = (string?)null,
            gradeId = Guid.NewGuid(),                 // not a writable field — must be ignored
            email = "hacker@example.com",             // not stored anywhere — must be ignored
        };

        var response = await client.PutAsJsonAsync("/api/me/profile", smuggled, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("\"email\"").And.NotContain("hacker@example.com");

        var updated = (await response.Content.ReadFromJsonAsync<StudentProfileResponse>(TestJson.Options))!;
        updated.GradeId.Should().Be(gradeId);          // unchanged, not the smuggled id

        // And the persisted grade is still the original.
        var storedGrade = await factory.QueryDbAsync(db => db.Students
            .IgnoreQueryFilters().Where(s => s.Id == student.Id).Select(s => s.GradeId).FirstAsync());
        storedGrade.Should().Be(gradeId);
    }

    // ── §B 400 — shape ───────────────────────────────────────────────────────
    [Fact]
    public async Task Put_400_on_empty_or_overlong_fields()
    {
        var (tenant, _, cityId, regionId, student) = await SetupAsync();
        var client = factory.CreateClientForStudent(tenant, student.Id);

        var nameTooLong = new string('a', 201);    // > MaximumLength(200)
        var phoneTooLong = new string('0', 33);    // > MaximumLength(32)

        // One negative case per §A.2 rule: NotEmpty + MaximumLength on the four required fields, plus the
        // optional secondary phone's length cap.
        UpdateMyStudentProfileBody[] invalid =
        [
            ValidBody(cityId, regionId) with { FullName = "" },
            ValidBody(cityId, regionId) with { FullName = nameTooLong },
            ValidBody(cityId, regionId) with { SchoolName = "" },
            ValidBody(cityId, regionId) with { SchoolName = nameTooLong },
            ValidBody(cityId, regionId) with { PhoneNumber = "" },
            ValidBody(cityId, regionId) with { PhoneNumber = phoneTooLong },
            ValidBody(cityId, regionId) with { ParentPhonePrimary = "" },
            ValidBody(cityId, regionId) with { ParentPhonePrimary = phoneTooLong },
            ValidBody(cityId, regionId) with { ParentPhoneSecondary = phoneTooLong },
        ];

        foreach (var body in invalid)
            (await client.PutAsJsonAsync("/api/me/profile", body, TestJson.Options))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── §B/§C.3 400 — unknown or mismatched city/region ──────────────────────
    [Fact]
    public async Task Put_400_on_unknown_city_or_mismatched_region()
    {
        var (tenant, _, cityId, regionId, student) = await SetupAsync();
        var client = factory.CreateClientForStudent(tenant, student.Id);

        // Unknown city.
        var unknownCity = ValidBody(Guid.NewGuid(), regionId);
        (await client.PutAsJsonAsync("/api/me/profile", unknownCity, TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Unknown region (valid city, but a regionId that does not exist) — the "must exist" half of §C.3.
        var unknownRegion = ValidBody(cityId, Guid.NewGuid());
        (await client.PutAsJsonAsync("/api/me/profile", unknownRegion, TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A region that belongs to a different city than the one supplied → mismatch.
        var (otherCity, otherRegion) = await OtherLocationAsync(cityId);
        otherCity.Should().NotBe(cityId);
        var mismatched = ValidBody(cityId, otherRegion);
        (await client.PutAsJsonAsync("/api/me/profile", mismatched, TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Nothing was persisted by the rejected writes.
        var reread = await GetProfileAsync(factory.CreateClientForStudent(tenant, student.Id));
        reread.FullName.Should().Be("Seed Student");
        reread.CityId.Should().Be(cityId);
    }

    // ── §B role gating ───────────────────────────────────────────────────────
    [Fact]
    public async Task Profile_is_student_only()
    {
        var (tenant, _, cityId, regionId, student) = await SetupAsync();
        var body = ValidBody(cityId, regionId);

        // Anonymous → 401 (both routes).
        var anon = factory.CreateClient();
        (await anon.GetAsync("/api/me/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PutAsJsonAsync("/api/me/profile", body, TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Staff token → 403 (both routes).
        var staff = factory.CreateClientFor(StaffRole.Teacher, tenant);
        (await staff.GetAsync("/api/me/profile")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.PutAsJsonAsync("/api/me/profile", body, TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Student token → 200.
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        (await studentClient.GetAsync("/api/me/profile")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await studentClient.PutAsJsonAsync("/api/me/profile", body, TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Cross-tenant isolation (NFR-SEC-010) ─────────────────────────────────
    [Fact]
    public async Task Profile_is_isolated_to_the_callers_tenant()
    {
        var (tenantA, _, cityA, regionA, studentA) = await SetupAsync();
        var (tenantB, _, _, _, studentB) = await SetupAsync();

        // A edits their own profile; B is untouched.
        (await factory.CreateClientForStudent(tenantA, studentA.Id)
            .PutAsJsonAsync("/api/me/profile", ValidBody(cityA, regionA) with { FullName = "Alice in Tenant A" },
                TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var a = await GetProfileAsync(factory.CreateClientForStudent(tenantA, studentA.Id));
        a.Id.Should().Be(studentA.Id);
        a.FullName.Should().Be("Alice in Tenant A");

        var b = await GetProfileAsync(factory.CreateClientForStudent(tenantB, studentB.Id));
        b.Id.Should().Be(studentB.Id);
        b.FullName.Should().Be("Seed Student");                 // B never saw A's write

        // A forged token carrying tenant A but student B's id cannot reach B's row — the global tenant filter
        // excludes it, so the subject resolves to nothing → 404 (cross-tenant access is impossible).
        (await factory.CreateClientForStudent(tenantA, studentB.Id).GetAsync("/api/me/profile"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
