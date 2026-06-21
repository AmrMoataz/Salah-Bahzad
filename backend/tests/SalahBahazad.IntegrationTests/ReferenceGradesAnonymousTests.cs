using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The new S1 anonymous, tenant-by-slug grades read for the sign-up wizard
/// (<c>GET /api/reference/grades?tenantSlug=</c>, FR-STU-REG-005). Proves it is reachable without a JWT,
/// returns only the tenant's live grades ordered by name, excludes soft-deleted grades, never leaks
/// another tenant's grades (NFR-SEC-010), and validates the slug.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReferenceGradesAnonymousTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Grades_are_listed_anonymously_ordered_by_name_excluding_soft_deleted()
    {
        var tenant = await factory.SeedTenantEntityAsync();
        var beta = await factory.SeedGradeAsync(tenant.Id, "Beta Grade");
        var alpha = await factory.SeedGradeAsync(tenant.Id, "Alpha Grade");
        var gone = await factory.SeedGradeAsync(tenant.Id, "Deleted Grade");

        // Soft-delete one grade — it must never reach the wizard.
        await factory.QueryDbAsync(async db =>
        {
            var grade = await db.Grades.IgnoreQueryFilters().FirstAsync(g => g.Id == gone.Id);
            grade.SoftDelete(Guid.NewGuid(), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            return 0;
        });

        var anon = factory.CreateClient(); // no Authorization header

        var grades = await anon.GetFromJsonAsync<List<GradeResponse>>(
            $"/api/reference/grades?tenantSlug={tenant.Slug}", TestJson.Options);

        grades.Should().NotBeNull();
        grades!.Select(g => g.Id).Should().Equal(alpha.Id, beta.Id); // ordered by Name, soft-deleted excluded
        grades.Should().NotContain(g => g.Id == gone.Id);
    }

    [Fact]
    public async Task Unknown_slug_returns_404()
    {
        var anon = factory.CreateClient();

        var response = await anon.GetAsync($"/api/reference/grades?tenantSlug=no-such-tenant-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Blank_tenant_slug_returns_400()
    {
        var anon = factory.CreateClient();

        var missing = await anon.GetAsync("/api/reference/grades");
        var blank = await anon.GetAsync("/api/reference/grades?tenantSlug=");

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tenant_slug_never_returns_another_tenants_grades()
    {
        var tenantA = await factory.SeedTenantEntityAsync();
        var tenantB = await factory.SeedTenantEntityAsync();
        var gradeA = await factory.SeedGradeAsync(tenantA.Id, "A Grade");
        var gradeB = await factory.SeedGradeAsync(tenantB.Id, "B Grade");

        var anon = factory.CreateClient();

        var grades = await anon.GetFromJsonAsync<List<GradeResponse>>(
            $"/api/reference/grades?tenantSlug={tenantA.Slug}", TestJson.Options);

        grades.Should().NotBeNull();
        grades!.Select(g => g.Id).Should().Contain(gradeA.Id);
        grades.Select(g => g.Id).Should().NotContain(gradeB.Id);
    }
}
