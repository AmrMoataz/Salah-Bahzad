using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Session CRUD lifecycle end-to-end (FR-ADM-SES-002/007/011): create → read → update → publish → archive
/// → soft-delete (hidden afterwards), with the create audited (FR-PLAT-SES-009).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SessionRoundTripTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Full_lifecycle_create_update_publish_archive_delete()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specializationId) = await factory.SeedTaxonomyAsync(tenant);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        // Create
        var create = await client.PostAsJsonAsync(
            "/api/sessions",
            new SaveSessionBody("Algebra I", "Intro to algebra", 150m, 120, gradeId, specializationId),
            TestJson.Options);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);
        created!.Title.Should().Be("Algebra I");
        created.Status.Should().Be("Draft");
        created.GradeName.Should().NotBeNullOrEmpty();
        created.SubjectName.Should().NotBeNullOrEmpty();
        created.SpecializationName.Should().NotBeNullOrEmpty();
        created.SubjectId.Should().NotBe(Guid.Empty);

        // Create is audited with a semantic action and Staff actor.
        var audit = await factory.LatestAuditAsync(tenant, "Session", "SessionCreated");
        audit.Should().NotBeNull();
        audit!.ActorType.Should().Be("Staff");

        // Read
        var detail = await client.GetFromJsonAsync<SessionDetailResponse>(
            $"/api/sessions/{created.Id}", TestJson.Options);
        detail!.Id.Should().Be(created.Id);

        // Update
        var update = await client.PutAsJsonAsync(
            $"/api/sessions/{created.Id}",
            new SaveSessionBody("Algebra I (rev)", null, 200m, 90, gradeId, specializationId),
            TestJson.Options);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);
        updated!.Title.Should().Be("Algebra I (rev)");
        updated.Description.Should().BeNull();
        updated.Price.Should().Be(200m);

        // Publish then archive
        (await client.PostAsync($"/api/sessions/{created.Id}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var archived = await (await client.PostAsync($"/api/sessions/{created.Id}/archive", null))
            .Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);
        archived!.Status.Should().Be("Archived");

        // Soft-delete → 204, then hidden (404)
        (await client.DeleteAsync($"/api/sessions/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/sessions/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // ...and absent from the list (soft-delete query filter).
        var page = await client.GetFromJsonAsync<PagedSessionResponse>("/api/sessions", TestJson.Options);
        page!.Items.Should().NotContain(i => i.Id == created.Id);
    }

    [Fact]
    public async Task List_filters_by_status_and_reports_stats()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, subjectId, specializationId) = await factory.SeedTaxonomyAsync(tenant);
        var published = await factory.SeedSessionAsync(tenant, gradeId, specializationId, SessionStatus.Published);
        await factory.SeedSessionAsync(tenant, gradeId, specializationId); // draft
        await factory.SeedQuestionAsync(tenant, published.Id);

        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var page = await client.GetFromJsonAsync<PagedSessionResponse>(
            $"/api/sessions?status=Published&subjectId={subjectId}", TestJson.Options);

        page!.Items.Should().ContainSingle();
        var row = page.Items[0];
        row.Id.Should().Be(published.Id);
        row.Status.Should().Be("Published");
        row.SubjectName.Should().NotBeNullOrEmpty();
        row.QuestionCount.Should().Be(1);
        row.EnrolledCount.Should().Be(0); // Phase 4
    }
}
