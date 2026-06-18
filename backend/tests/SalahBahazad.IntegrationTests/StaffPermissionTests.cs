using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Server-side permission enforcement (Part A1, FR-PLAT-AUTH-007/008, default-deny): anonymous → 401,
/// Assistant may read but not write, Teacher may read.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StaffPermissionTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Anonymous_request_is_rejected_with_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/staff");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assistant_can_read_but_cannot_write()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Assistant, tenantId);

        var list = await client.GetAsync("/api/staff");
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await client.PostAsJsonAsync("/api/staff",
            new { displayName = "New Person", email = $"new-{Guid.NewGuid():N}@x.com", role = "Assistant" });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var update = await client.PutAsJsonAsync($"/api/staff/{Guid.NewGuid()}",
            new { displayName = "X", email = "x@x.com", role = "Assistant" });
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var delete = await client.DeleteAsync($"/api/staff/{Guid.NewGuid()}");
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_can_read()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.GetAsync("/api/staff");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
