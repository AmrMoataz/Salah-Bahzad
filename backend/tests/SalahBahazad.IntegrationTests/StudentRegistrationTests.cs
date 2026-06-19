using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
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
