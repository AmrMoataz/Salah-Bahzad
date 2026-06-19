using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Video/material/thumbnail uploads go through the real R2 path (MinIO Testcontainer): the source is
/// stored, the stubbed transcode marks the video Ready, and a material's signed URL serves the stored bytes
/// (FR-ADM-SES-003/004, FR-PLAT-AST-003, FR-PLAT-VID-007).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SessionContentTests(SalahBahazadApiFactory factory)
{
    private static readonly byte[] VideoBytes = [0x00, 0x00, 0x00, 0x18, 1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 9, 8, 7, 6, 5];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];

    [Fact]
    public async Task Add_video_uploads_source_and_stub_marks_it_ready()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        using var form = new MultipartFormDataContent
        {
            { new StringContent("Lesson 1"), "title" },
            { new StringContent("8"), "lengthMinutes" },
            { new StringContent("3"), "accessCount" },
        };
        var file = new ByteArrayContent(VideoBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(file, "file", "lesson1.mp4");

        var response = await client.PostAsync($"/api/sessions/{session.Id}/videos", form);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SessionVideoResponse>(TestJson.Options);
        created!.Title.Should().Be("Lesson 1");
        created.LengthMinutes.Should().Be(8);
        created.AccessCount.Should().Be(3);
        created.ProcessingStatus.Should().Be("Pending"); // 201 snapshot per the contract

        // The stub transcode flips it to Ready, observable on the next read.
        var detail = await client.GetFromJsonAsync<SessionDetailResponse>(
            $"/api/sessions/{session.Id}", TestJson.Options);
        detail!.Videos.Should().ContainSingle();
        detail.Videos[0].ProcessingStatus.Should().Be("Ready");
    }

    [Fact]
    public async Task Add_material_then_signed_url_serves_the_stored_bytes()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "notes.pdf");

        var add = await client.PostAsync($"/api/sessions/{session.Id}/materials", form);
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        var material = await add.Content.ReadFromJsonAsync<SessionMaterialResponse>(TestJson.Options);
        material!.FileName.Should().Be("notes.pdf");
        material.Kind.Should().Be("PDF");

        // Signed URL serves the exact bytes we uploaded (proves the round trip to MinIO/R2).
        var signed = await client.GetFromJsonAsync<SignedUrlResponse>(
            $"/api/sessions/{session.Id}/materials/{material.Id}/url", TestJson.Options);
        signed!.Url.Should().NotBeNullOrWhiteSpace();

        using var http = new HttpClient();
        var download = await http.GetAsync(signed.Url);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);

        // Remove it.
        (await client.DeleteAsync($"/api/sessions/{session.Id}/materials/{material.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Set_thumbnail_embeds_a_signed_url_in_the_detail()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(PngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "thumb.png");

        var response = await client.PutAsync($"/api/sessions/{session.Id}/thumbnail", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<SessionDetailResponse>(TestJson.Options);
        detail!.ThumbnailUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Add_video_rejects_a_disallowed_file_type()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenant);

        using var form = new MultipartFormDataContent
        {
            { new StringContent("Bad"), "title" },
            { new StringContent("5"), "lengthMinutes" },
            { new StringContent("1"), "accessCount" },
        };
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "bad.pdf");

        (await client.PostAsync($"/api/sessions/{session.Id}/videos", form))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
