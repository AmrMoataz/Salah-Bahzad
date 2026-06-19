using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Question-bank authoring end-to-end (FR-ADM-QB-001..006): create with MCQ options, list, add a variation,
/// upload an image (signed URL embedded), soft-delete ("detach") hides the row, and the option invariant is
/// enforced (≥ 2 options, exactly one correct → 400).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class QuestionBankTests(SalahBahazadApiFactory factory)
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 5, 6, 7, 8];

    [Fact]
    public async Task Create_list_vary_image_and_softdelete()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        // Create
        var create = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/questions",
            new SaveQuestionBody("2 + 2 = ?", 2, true, "https://youtu.be/x",
                [new OptionBody("3", false), new OptionBody("4", true)]),
            TestJson.Options);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var question = await create.Content.ReadFromJsonAsync<QuestionResponse>(TestJson.Options);
        question!.Options.Should().HaveCount(2);
        question.Options.Count(o => o.IsCorrect).Should().Be(1);
        question.Mark.Should().Be(2);

        // List
        var page = await client.GetFromJsonAsync<PagedQuestionResponse>(
            $"/api/sessions/{session.Id}/questions", TestJson.Options);
        page!.Items.Should().ContainSingle(q => q.Id == question.Id);

        // Add a variation
        var addVariation = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/questions/{question.Id}/variations",
            new SaveVariationBody("two plus two", [new OptionBody("4", true), new OptionBody("5", false)]),
            TestJson.Options);
        addVariation.StatusCode.Should().Be(HttpStatusCode.Created);
        var variation = await addVariation.Content.ReadFromJsonAsync<VariationResponse>(TestJson.Options);
        variation!.Options.Should().HaveCount(2);

        // Upload a question image → embedded signed URL.
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(PngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "q.png");
        var image = await client.PutAsync(
            $"/api/sessions/{session.Id}/questions/{question.Id}/image", form);
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        var withImage = await image.Content.ReadFromJsonAsync<QuestionResponse>(TestJson.Options);
        withImage!.ImageUrl.Should().NotBeNullOrWhiteSpace();

        // Soft-delete ("detach") → hidden from the list.
        (await client.DeleteAsync($"/api/sessions/{session.Id}/questions/{question.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await client.GetFromJsonAsync<PagedQuestionResponse>(
            $"/api/sessions/{session.Id}/questions", TestJson.Options);
        after!.Items.Should().NotContain(q => q.Id == question.Id);
    }

    [Theory]
    [InlineData(1, true)]   // only one option
    [InlineData(2, false)]  // two options, none correct
    public async Task Create_enforces_option_invariants(int optionCount, bool firstCorrect)
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var options = new List<OptionBody> { new("A", firstCorrect) };
        if (optionCount == 2) options.Add(new OptionBody("B", false));

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/questions",
            new SaveQuestionBody("body", 1, true, null, options),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
