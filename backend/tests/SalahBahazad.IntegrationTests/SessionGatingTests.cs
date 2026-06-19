using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Prerequisite and gating-quiz rules: self/cycle prerequisites are rejected (FR-ADM-SES-005), quiz-setting
/// ranges are validated (FR-ADM-QZ-001), and publish is hard-blocked when the quiz needs more questions than
/// the bank has eligible (FR-ADM-QZ-002, per the frozen contract — publish → 409).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SessionGatingTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Prerequisite_self_reference_is_409()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{session.Id}/prerequisite", new PrerequisiteBody(session.Id), TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Prerequisite_cycle_is_409()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var a = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var b = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        // A requires B — fine.
        (await client.PutAsJsonAsync(
                $"/api/sessions/{a.Id}/prerequisite", new PrerequisiteBody(b.Id), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // B requires A — would close the loop → 409.
        (await client.PutAsJsonAsync(
                $"/api/sessions/{b.Id}/prerequisite", new PrerequisiteBody(a.Id), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Quiz_settings_out_of_range_is_400()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{session.Id}/quiz-settings",
            new QuizSettingsBody(TimeLimitMinutes: 1, QuestionCount: 10, AttemptCount: 2, MinPassPercent: 60),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Publish_is_blocked_until_enough_eligible_questions_exist()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        // Quiz wants 5 questions, but the bank has none eligible yet.
        (await client.PutAsJsonAsync(
                $"/api/sessions/{session.Id}/quiz-settings",
                new QuizSettingsBody(15, 5, 2, 60), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsync($"/api/sessions/{session.Id}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Add 5 quiz-eligible questions, then publish succeeds.
        for (var i = 0; i < 5; i++)
            await factory.SeedQuestionAsync(tenant, session.Id, isValidForQuiz: true);

        (await client.PostAsync($"/api/sessions/{session.Id}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
