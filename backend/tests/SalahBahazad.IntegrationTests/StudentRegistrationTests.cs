using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Anonymous self-registration end-to-end through the real R2 path (MinIO Testcontainer): the request
/// creates a Pending student, uploads the ID image to the private bucket, and is audited. Staff can
/// then fetch a signed URL (audited) that actually serves the stored bytes
/// (FR-STU-REG-001..008, FR-PLAT-AST-003/004).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StudentRegistrationTests(SalahBahazadApiFactory factory)
{
    private static readonly byte[] ImageBytes = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    [Fact]
    public async Task Register_creates_pending_student_uploads_id_image_and_is_audited()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantEntityAsync();
        var grade = await factory.SeedGradeAsync(tenant.Id);

        using var form = new MultipartFormDataContent
        {
            { new StringContent("firebase-id-token"), "firebaseIdToken" },
            { new StringContent(tenant.Slug), "tenantSlug" },
            { new StringContent("Mariam Adel"), "fullName" },
            { new StringContent("01099999999"), "phoneNumber" },
            { new StringContent("01000000000"), "parentPhonePrimary" },
            { new StringContent(grade.Id.ToString()), "gradeId" },
            { new StringContent(cityId.ToString()), "cityId" },
            { new StringContent(regionId.ToString()), "regionId" },
            { new StringContent("Nile Language School"), "schoolName" },
            { new StringContent("terms-v1"), "termsVersion" },
        };
        var file = new ByteArrayContent(ImageBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "idImage", "id.jpg");

        var anon = factory.CreateClient();
        var register = await anon.PostAsync("/api/students/register", form);
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await register.Content.ReadFromJsonAsync<StudentRegistrationResponse>(TestJson.Options);
        result!.Status.Should().Be("Pending");

        // Registration is audited (anonymous → written explicitly with the resolved tenant).
        var regAudit = await factory.LatestAuditAsync(tenant.Id, "Student", "StudentRegistered");
        regAudit.Should().NotBeNull();
        regAudit!.ActorType.Should().Be("Student");

        // Staff see a pending student with an ID image attached.
        var staff = factory.CreateClientFor(StaffRole.Teacher, tenant.Id);
        var detail = await staff.GetFromJsonAsync<StudentDetailResponse>(
            $"/api/students/{result.StudentId}", TestJson.Options);
        detail!.Status.Should().Be("Pending");
        detail.HasIdImage.Should().BeTrue();

        // The signed-URL endpoint audits the access and returns a working URL.
        var urlResp = await staff.GetAsync($"/api/students/{result.StudentId}/id-image");
        urlResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var signed = await urlResp.Content.ReadFromJsonAsync<StudentIdImageUrlResponse>(TestJson.Options);
        signed!.Url.Should().NotBeNullOrWhiteSpace();

        var viewAudit = await factory.LatestAuditAsync(tenant.Id, "Student", "StudentIdImageViewed");
        viewAudit.Should().NotBeNull();

        // The signed URL actually serves the bytes we uploaded (proves the round trip to MinIO/R2).
        using var http = new HttpClient();
        var download = await http.GetAsync(signed.Url);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(ImageBytes);
    }

    [Fact]
    public async Task Rejected_student_can_resubmit_reusing_the_same_account()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantEntityAsync();
        var grade = await factory.SeedGradeAsync(tenant.Id);

        // Pin a token → UID so both registrations resolve to the *same* Firebase identity (the default
        // fake yields a fresh UID per call). This is what lets the second submit find the rejected row.
        const string token = "resubmit-firebase-token";
        factory.PinFirebaseUser(token, $"resubmit-uid-{Guid.NewGuid():N}");
        var anon = factory.CreateClient();

        // 1) First registration → Pending.
        var first = await anon.PostAsync("/api/students/register", BuildForm(token, tenant.Slug, grade.Id, cityId, regionId, "Mariam Adel"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstResult = await first.Content.ReadFromJsonAsync<StudentRegistrationResponse>(TestJson.Options);
        var studentId = firstResult!.StudentId;

        // 2) Staff reject with a reason.
        var staff = factory.CreateClientFor(StaffRole.Teacher, tenant.Id);
        var reject = await staff.PostAsJsonAsync($"/api/students/{studentId}/reject", new { reason = "Blurry ID photo" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3) Re-submit with the same account but corrected details — reuses the row, no 409.
        var resubmit = await anon.PostAsync("/api/students/register", BuildForm(token, tenant.Slug, grade.Id, cityId, regionId, "Mariam Adel Hassan"));
        resubmit.StatusCode.Should().Be(HttpStatusCode.Created);
        var resubmitResult = await resubmit.Content.ReadFromJsonAsync<StudentRegistrationResponse>(TestJson.Options);
        resubmitResult!.StudentId.Should().Be(studentId, "the rejected row is reused, not a new one");
        resubmitResult.Status.Should().Be("Pending");

        // Re-submission is audited under its own action.
        var resubmitAudit = await factory.LatestAuditAsync(tenant.Id, "Student", "StudentResubmitted");
        resubmitAudit.Should().NotBeNull();
        resubmitAudit!.ActorType.Should().Be("Student");

        // The student is Pending again, the rejection reason is cleared, and the corrected name took.
        var detail = await staff.GetFromJsonAsync<StudentDetailResponse>(
            $"/api/students/{studentId}", TestJson.Options);
        detail!.Status.Should().Be("Pending");
        detail.RejectionReason.Should().BeNull();
        detail.FullName.Should().Be("Mariam Adel Hassan");
    }

    [Fact]
    public async Task Register_mints_a_distinct_STU_serial_per_student()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantEntityAsync();
        var grade = await factory.SeedGradeAsync(tenant.Id);

        // Two distinct Firebase identities → two separate students in the same tenant.
        factory.PinFirebaseUser("serial-token-a", $"serial-uid-a-{Guid.NewGuid():N}");
        factory.PinFirebaseUser("serial-token-b", $"serial-uid-b-{Guid.NewGuid():N}");
        var anon = factory.CreateClient();

        var a = await anon.PostAsync("/api/students/register",
            BuildForm("serial-token-a", tenant.Slug, grade.Id, cityId, regionId, "Student A"));
        var b = await anon.PostAsync("/api/students/register",
            BuildForm("serial-token-b", tenant.Slug, grade.Id, cityId, regionId, "Student B"));
        a.StatusCode.Should().Be(HttpStatusCode.Created);
        b.StatusCode.Should().Be(HttpStatusCode.Created);

        var idA = (await a.Content.ReadFromJsonAsync<StudentRegistrationResponse>(TestJson.Options))!.StudentId;
        var idB = (await b.Content.ReadFromJsonAsync<StudentRegistrationResponse>(TestJson.Options))!.StudentId;

        // Read the serials straight from the row (no HTTP context → tenant filter resolves to Empty → ignore it).
        var serials = await factory.QueryDbAsync(db => db.Students
            .IgnoreQueryFilters()
            .Where(s => s.Id == idA || s.Id == idB)
            .Select(s => s.Serial)
            .ToListAsync());

        serials.Should().HaveCount(2);
        serials.Should().OnlyContain(s => s.StartsWith("STU-"));
        // The handler's NextUnique seeding + the (TenantId, Serial) unique index guarantee per-tenant uniqueness
        // (a collision would have failed the second insert).
        serials.Should().OnlyHaveUniqueItems();
    }

    private static MultipartFormDataContent BuildForm(
        string firebaseToken, string tenantSlug, Guid gradeId, Guid cityId, Guid regionId, string fullName)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(firebaseToken), "firebaseIdToken" },
            { new StringContent(tenantSlug), "tenantSlug" },
            { new StringContent(fullName), "fullName" },
            { new StringContent("01099999999"), "phoneNumber" },
            { new StringContent("01000000000"), "parentPhonePrimary" },
            { new StringContent(gradeId.ToString()), "gradeId" },
            { new StringContent(cityId.ToString()), "cityId" },
            { new StringContent(regionId.ToString()), "regionId" },
            { new StringContent("Nile Language School"), "schoolName" },
            { new StringContent("terms-v1"), "termsVersion" },
        };
        var file = new ByteArrayContent(ImageBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "idImage", "id.jpg");
        return form;
    }

    [Fact]
    public async Task Register_rejects_a_disallowed_file_type()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenant = await factory.SeedTenantEntityAsync();
        var grade = await factory.SeedGradeAsync(tenant.Id);

        using var form = new MultipartFormDataContent
        {
            { new StringContent("firebase-id-token"), "firebaseIdToken" },
            { new StringContent(tenant.Slug), "tenantSlug" },
            { new StringContent("Omar Khaled"), "fullName" },
            { new StringContent("01099999999"), "phoneNumber" },
            { new StringContent("01000000000"), "parentPhonePrimary" },
            { new StringContent(grade.Id.ToString()), "gradeId" },
            { new StringContent(cityId.ToString()), "cityId" },
            { new StringContent(regionId.ToString()), "regionId" },
            { new StringContent("Some School"), "schoolName" },
            { new StringContent("terms-v1"), "termsVersion" },
        };
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "idImage", "id.pdf");

        var anon = factory.CreateClient();
        var register = await anon.PostAsync("/api/students/register", form);

        register.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
