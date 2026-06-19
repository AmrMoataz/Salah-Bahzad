using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>Student listing is tenant-isolated through the EF global query filter (NFR-SEC-010).</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentTenantIsolationTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Listing_returns_only_the_callers_tenant()
    {
        var (cityId, regionId) = await factory.GetSeedLocationAsync();

        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();
        var gradeA = await factory.SeedGradeAsync(tenantA);
        var gradeB = await factory.SeedGradeAsync(tenantB);

        var a1 = await factory.SeedStudentAsync(tenantA, gradeA.Id, cityId, regionId);
        var a2 = await factory.SeedStudentAsync(tenantA, gradeA.Id, cityId, regionId);
        await factory.SeedStudentAsync(tenantB, gradeB.Id, cityId, regionId);
        await factory.SeedStudentAsync(tenantB, gradeB.Id, cityId, regionId);

        var client = factory.CreateClientFor(StaffRole.Teacher, tenantA);
        var page = await client.GetFromJsonAsync<PagedStudentResponse>("/api/students", TestJson.Options);

        page.Should().NotBeNull();
        page!.Total.Should().Be(2);
        page.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { a1.Id, a2.Id });
    }
}
