using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>Session listing is tenant-isolated through the EF global query filter (NFR-SEC-010).</summary>
[Collection(ApiCollection.Name)]
public sealed class SessionTenantIsolationTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Listing_returns_only_the_callers_tenant()
    {
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();
        var (gradeA, _, specA) = await factory.SeedTaxonomyAsync(tenantA);
        var (gradeB, _, specB) = await factory.SeedTaxonomyAsync(tenantB);

        var a1 = await factory.SeedSessionAsync(tenantA, gradeA, specA);
        var a2 = await factory.SeedSessionAsync(tenantA, gradeA, specA);
        await factory.SeedSessionAsync(tenantB, gradeB, specB);

        var client = factory.CreateClientFor(StaffRole.Teacher, tenantA);
        var page = await client.GetFromJsonAsync<PagedSessionResponse>("/api/sessions", TestJson.Options);

        page!.Total.Should().Be(2);
        page.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { a1.Id, a2.Id });
    }

    [Fact]
    public async Task Cannot_read_another_tenants_session_by_id()
    {
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();
        var (gradeB, _, specB) = await factory.SeedTaxonomyAsync(tenantB);
        var bSession = await factory.SeedSessionAsync(tenantB, gradeB, specB);

        var clientA = factory.CreateClientFor(StaffRole.Teacher, tenantA);
        var response = await clientA.GetAsync($"/api/sessions/{bSession.Id}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
