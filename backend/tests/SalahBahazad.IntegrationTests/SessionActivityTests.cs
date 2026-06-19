using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The session detail Activity feed (FR-PLAT-SES-009) surfaces both root-level changes and content changes
/// (videos/materials/questions) as session-keyed, human-readable rows — content handlers write an explicit
/// session-scoped audit entry on top of the interceptor's per-child field-diff.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SessionActivityTests(SalahBahazadApiFactory factory)
{
    private static readonly byte[] VideoBytes = [0x00, 0x00, 0x00, 0x18, 1, 2, 3, 4];

    [Fact]
    public async Task Activity_feed_includes_root_and_content_actions()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        // Create a session → SessionCreated
        var create = await client.PostAsJsonAsync(
            "/api/sessions",
            new SaveSessionBody("Audited session", null, 0m, 30, gradeId, specId),
            TestJson.Options);
        var session = await create.Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);

        // Add a video → SessionVideoAdded
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Lesson 1"), "title" },
            { new StringContent("8"), "lengthMinutes" },
            { new StringContent("2"), "accessCount" },
        };
        var file = new ByteArrayContent(VideoBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(file, "file", "lesson1.mp4");
        await client.PostAsync($"/api/sessions/{session!.Id}/videos", form);

        // Add a question → QuestionAdded
        await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/questions",
            new SaveQuestionBody("2 + 2 = ?", 1, true, null,
                [new OptionBody("4", true), new OptionBody("3", false)]),
            TestJson.Options);

        // The feed surfaces all three actions, keyed to the session, attributed to staff.
        var feed = await client.GetFromJsonAsync<PagedSessionActivityResponse>(
            $"/api/sessions/{session.Id}/activity", TestJson.Options);

        var actions = feed!.Items.Select(i => i.Action).ToList();
        actions.Should().Contain("SessionCreated");
        actions.Should().Contain("SessionVideoAdded");
        actions.Should().Contain("QuestionAdded");
        feed.Items.Should().Contain(i => i.Summary == "Video added: Lesson 1");
        feed.Items.Should().OnlyContain(i => i.ActorType == "Staff");
    }

    [Fact]
    public async Task Activity_feed_reads_root_edits_semantically()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var prereqCreate = await client.PostAsJsonAsync(
            "/api/sessions",
            new SaveSessionBody("Prerequisite", null, 0m, 30, gradeId, specId),
            TestJson.Options);
        var prereq = await prereqCreate.Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);

        var create = await client.PostAsJsonAsync(
            "/api/sessions",
            new SaveSessionBody("Main", null, 0m, 30, gradeId, specId),
            TestJson.Options);
        var session = await create.Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);

        // Edit details → SessionUpdated (not the interceptor's generic "Updated Session")
        await client.PutAsJsonAsync(
            $"/api/sessions/{session!.Id}",
            new SaveSessionBody("Main (renamed)", "a description", 50m, 60, gradeId, specId),
            TestJson.Options);

        // Set a prerequisite → SessionPrerequisiteChanged
        await client.PutAsJsonAsync(
            $"/api/sessions/{session.Id}/prerequisite",
            new PrerequisiteBody(prereq!.Id),
            TestJson.Options);

        // Upload a thumbnail → SessionThumbnailUpdated
        using var form = new MultipartFormDataContent();
        var img = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4]);
        img.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(img, "file", "thumb.png");
        await client.PutAsync($"/api/sessions/{session.Id}/thumbnail", form);

        var feed = await client.GetFromJsonAsync<PagedSessionActivityResponse>(
            $"/api/sessions/{session.Id}/activity?pageSize=50", TestJson.Options);
        var actions = feed!.Items.Select(i => i.Action).ToList();

        actions.Should().Contain("SessionUpdated");
        actions.Should().Contain("SessionPrerequisiteChanged");
        actions.Should().Contain("SessionThumbnailUpdated");
    }
}
