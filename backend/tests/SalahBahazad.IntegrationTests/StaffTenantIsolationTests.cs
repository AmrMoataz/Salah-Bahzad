using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>Tenant isolation proven end-to-end through the EF global query filter (NFR-SEC-010).</summary>
[Collection(ApiCollection.Name)]
public sealed class StaffTenantIsolationTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Listing_returns_only_the_callers_tenant()
    {
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();

        var a1 = await factory.SeedStaffAsync(tenantA, StaffRole.Assistant);
        var a2 = await factory.SeedStaffAsync(tenantA, StaffRole.Teacher);
        await factory.SeedStaffAsync(tenantB, StaffRole.Assistant);
        await factory.SeedStaffAsync(tenantB, StaffRole.Teacher);
        await factory.SeedStaffAsync(tenantB, StaffRole.Assistant);

        var client = factory.CreateClientFor(StaffRole.Teacher, tenantA);
        var page = await client.GetFromJsonAsync<PagedStaffResponse>("/api/staff", TestJson.Options);

        page.Should().NotBeNull();
        page!.Total.Should().Be(2);
        page.Items.Select(i => i.Email).Should().BeEquivalentTo(new[] { a1.Email, a2.Email });
    }
}
