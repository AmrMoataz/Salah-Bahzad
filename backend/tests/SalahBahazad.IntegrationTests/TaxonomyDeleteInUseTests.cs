using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Delete-in-use rule end-to-end (FR-PLAT-TAX-004): a subject with live specializations is blocked
/// with 409; once its specializations are removed it soft-deletes with 204.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaxonomyDeleteInUseTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Deleting_a_subject_in_use_returns_409_then_succeeds_once_freed()
    {
        var tenantId = await factory.SeedTenantAsync();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId);

        var subjectResp = await client.PostAsJsonAsync(
            "/api/taxonomy/subjects", new { name = $"Chemistry {Guid.NewGuid():N}" });
        var subject = await subjectResp.Content.ReadFromJsonAsync<SubjectResponse>(TestJson.Options);

        var specResp = await client.PostAsJsonAsync(
            "/api/taxonomy/specializations", new { subjectId = subject!.Id, name = "Organic" });
        var spec = await specResp.Content.ReadFromJsonAsync<SpecializationResponse>(TestJson.Options);

        // In use → blocked.
        var blocked = await client.DeleteAsync($"/api/taxonomy/subjects/{subject.Id}");
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Remove the specialization, then the subject is free to delete.
        var deleteSpec = await client.DeleteAsync($"/api/taxonomy/specializations/{spec!.Id}");
        deleteSpec.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteSubject = await client.DeleteAsync($"/api/taxonomy/subjects/{subject.Id}");
        deleteSubject.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Soft-deleted: it no longer appears in the list.
        var subjects = await client.GetFromJsonAsync<List<SubjectResponse>>("/api/taxonomy/subjects", TestJson.Options);
        subjects!.Should().NotContain(s => s.Id == subject.Id);
    }
}
