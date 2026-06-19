using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Server-side permission enforcement for taxonomy (FR-PLAT-AUTH-007/008, default-deny): anonymous →
/// 401; Assistant may read but not write (writes are Teacher-only); Teacher may read and write.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaxonomyPermissionTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Anonymous_taxonomy_request_is_rejected_with_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/taxonomy/grades");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assistant_can_read_but_cannot_write_taxonomy()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Assistant, tenantId);

        var read = await client.GetAsync("/api/taxonomy/grades");
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await client.PostAsJsonAsync("/api/taxonomy/grades", new { name = "Grade 12" });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var update = await client.PutAsJsonAsync($"/api/taxonomy/grades/{Guid.NewGuid()}", new { name = "X" });
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var delete = await client.DeleteAsync($"/api/taxonomy/grades/{Guid.NewGuid()}");
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_can_read_and_write_taxonomy()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var read = await client.GetAsync("/api/taxonomy/subjects");
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await client.PostAsJsonAsync(
            "/api/taxonomy/subjects", new { name = $"Biology {Guid.NewGuid():N}" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
