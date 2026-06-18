using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>Global exception → HTTP status mapping (Part A2): 400 (+errors), 404, 409.</summary>
[Collection(ApiCollection.Name)]
public sealed class StaffExceptionMappingTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Invalid_body_returns_400_with_field_errors()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.PostAsJsonAsync("/api/staff",
            new { displayName = "", email = "not-an-email", role = "Assistant" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(TestJson.Options);
        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey("DisplayName");
        problem.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var response = await client.GetAsync($"/api/staff/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_email_returns_409()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);
        var email = $"dupe-{Guid.NewGuid():N}@x.com";

        var first = await client.PostAsJsonAsync("/api/staff",
            new { displayName = "First", email, role = "Assistant" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/staff",
            new { displayName = "Second", email, role = "Assistant" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
