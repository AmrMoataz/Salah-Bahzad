using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Student lifecycle through the real API: pending → active, reject-with-mandatory-reason, and
/// device-clear — each audited with the semantic action/reason (FR-ADM-STU-003/004/010, FR-PLAT-DEV-004).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StudentLifecycleTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Approve_moves_pending_to_active_and_is_audited()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenantId = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenantId);
        var student = await factory.SeedStudentAsync(tenantId, grade.Id, cityId, regionId);
        var teacherId = Guid.NewGuid();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId, teacherId);

        var response = await client.PostAsync($"/api/students/{student.Id}/approve", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<StudentDetailResponse>(TestJson.Options);
        detail!.Status.Should().Be("Active");

        var audit = await factory.LatestAuditAsync(tenantId, "Student", "StudentApproved");
        audit.Should().NotBeNull();
        audit!.ActorId.Should().Be(teacherId);
        audit.ActorType.Should().Be("Staff");
    }

    [Fact]
    public async Task Reject_requires_a_reason()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenantId = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenantId);
        var student = await factory.SeedStudentAsync(tenantId, grade.Id, cityId, regionId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.PostAsJsonAsync($"/api/students/{student.Id}/reject", new { reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_with_reason_stores_reason_and_audits_it()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenantId = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenantId);
        var student = await factory.SeedStudentAsync(tenantId, grade.Id, cityId, regionId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.PostAsJsonAsync(
            $"/api/students/{student.Id}/reject", new { reason = "Blurry ID photo" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<StudentDetailResponse>(TestJson.Options);
        detail!.Status.Should().Be("Rejected");
        detail.RejectionReason.Should().Be("Blurry ID photo");

        var audit = await factory.LatestAuditAsync(tenantId, "Student", "StudentRejected");
        audit.Should().NotBeNull();
        audit!.Summary.Should().Contain("Blurry ID photo");
    }

    [Fact]
    public async Task Clear_device_deactivates_it_and_audits_the_reason()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenantId = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenantId);
        var student = await factory.SeedStudentAsync(tenantId, grade.Id, cityId, regionId, StudentStatus.Active);
        await factory.SeedDeviceAsync(tenantId, student.Id);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.PostAsJsonAsync(
            $"/api/students/{student.Id}/clear-device", new { reason = "Lost phone" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<StudentDetailResponse>(TestJson.Options);
        detail!.ActiveDevice.Should().BeNull(); // no active device remains after clearing

        var audit = await factory.LatestAuditAsync(tenantId, "StudentDevice", "StudentDeviceCleared");
        audit.Should().NotBeNull();
        audit!.Summary.Should().Contain("Lost phone");
    }

    [Fact]
    public async Task Clear_device_returns_409_when_no_active_device()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var tenantId = await factory.SeedTenantAsync();
        var grade = await factory.SeedGradeAsync(tenantId);
        var student = await factory.SeedStudentAsync(tenantId, grade.Id, cityId, regionId, StudentStatus.Active);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.PostAsJsonAsync(
            $"/api/students/{student.Id}/clear-device", new { reason = "no device" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
