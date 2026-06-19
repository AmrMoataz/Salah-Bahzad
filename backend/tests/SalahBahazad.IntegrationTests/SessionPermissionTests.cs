using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Server-side permission enforcement (default-deny, NFR-SEC-003). An Assistant authors session content
/// (read/create/edit) but cannot publish to the catalogue or delete — those stay Teacher-only
/// (FR-PLAT-AUTH-007, FR-PLAT-ROLE-003). An unauthenticated caller is rejected before any handler runs.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SessionPermissionTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Unauthenticated_caller_is_401()
    {
        var anon = factory.CreateClient();
        (await anon.GetAsync("/api/sessions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assistant_can_create_and_edit_but_not_publish_or_delete()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specializationId) = await factory.SeedTaxonomyAsync(tenant);
        var assistant = factory.CreateClientFor(StaffRole.Assistant, tenant);

        // Read is allowed.
        (await assistant.GetAsync("/api/sessions")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Create + edit are allowed (content authoring).
        var create = await assistant.PostAsJsonAsync(
            "/api/sessions",
            new SaveSessionBody("Assistant draft", null, 0m, 30, gradeId, specializationId),
            TestJson.Options);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);

        var edit = await assistant.PutAsJsonAsync(
            $"/api/sessions/{created!.Id}",
            new SaveSessionBody("Assistant draft (edited)", null, 0m, 30, gradeId, specializationId),
            TestJson.Options);
        edit.StatusCode.Should().Be(HttpStatusCode.OK);

        // Publish (go-live) and delete remain Teacher-only → forbidden.
        (await assistant.PostAsync($"/api/sessions/{created.Id}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await assistant.DeleteAsync($"/api/sessions/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
