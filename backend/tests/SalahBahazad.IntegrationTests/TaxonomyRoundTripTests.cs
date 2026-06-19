using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Taxonomy CRUD round trips through the real API: create → list, the Subject → Specialization
/// hierarchy with its live-count, and proof that a create is audited (FR-PLAT-TAX-001/002, FR-ADM-TAX-*).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaxonomyRoundTripTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Create_grade_appears_in_list_and_is_audited()
    {
        var tenantId = await factory.SeedTenantAsync();
        var teacherId = Guid.NewGuid();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId, teacherId);

        var name = $"Grade {Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/taxonomy/grades", new { name });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<GradeResponse>(TestJson.Options);
        created!.Name.Should().Be(name);

        var grades = await client.GetFromJsonAsync<List<GradeResponse>>("/api/taxonomy/grades", TestJson.Options);
        grades!.Should().ContainSingle(g => g.Id == created.Id && g.Name == name);

        var audit = await factory.LatestAuditAsync(tenantId, "Grade", "Created");
        audit.Should().NotBeNull();
        audit!.ActorId.Should().Be(teacherId);
        audit.ActorType.Should().Be("Staff");
    }

    [Fact]
    public async Task Subject_specialization_hierarchy_round_trips_with_live_count()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        // Create a subject — starts with zero specializations.
        var subjectName = $"Physics {Guid.NewGuid():N}";
        var subjectResp = await client.PostAsJsonAsync("/api/taxonomy/subjects", new { name = subjectName });
        subjectResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var subject = await subjectResp.Content.ReadFromJsonAsync<SubjectResponse>(TestJson.Options);
        subject!.SpecializationCount.Should().Be(0);

        // Add a specialization under it — the response carries the owning subject name.
        var specResp = await client.PostAsJsonAsync(
            "/api/taxonomy/specializations", new { subjectId = subject.Id, name = "Mechanics" });
        specResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var spec = await specResp.Content.ReadFromJsonAsync<SpecializationResponse>(TestJson.Options);
        spec!.SubjectId.Should().Be(subject.Id);
        spec.SubjectName.Should().Be(subjectName);

        // The subject list now reports the live specialization count.
        var subjects = await client.GetFromJsonAsync<List<SubjectResponse>>("/api/taxonomy/subjects", TestJson.Options);
        subjects!.Single(s => s.Id == subject.Id).SpecializationCount.Should().Be(1);

        // The specialization list filtered by subject returns exactly that specialization.
        var specs = await client.GetFromJsonAsync<List<SpecializationResponse>>(
            $"/api/taxonomy/specializations?subjectId={subject.Id}", TestJson.Options);
        specs!.Should().ContainSingle(s => s.Id == spec.Id);
    }
}
