using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Server-side permission enforcement for students (default-deny, FR-PLAT-AUTH-007/008): anonymous →
/// 401; an Assistant may read and approve but not deactivate (Teacher-only); a Teacher may read.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StudentPermissionTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Anonymous_request_is_rejected_with_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/students");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assistant_can_read_but_cannot_deactivate()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Assistant, tenantId);

        var list = await client.GetAsync("/api/students");
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        // StudentsDeactivate is Teacher-only — Assistant is forbidden (before any not-found check).
        var deactivate = await client.PostAsJsonAsync(
            $"/api/students/{Guid.NewGuid()}/active", new { isActive = false });
        deactivate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_can_read()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.GetAsync("/api/students");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
