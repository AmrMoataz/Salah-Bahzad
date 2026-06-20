using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Code register, generation, export, lifecycle, tenant isolation and default-deny (contract §2,
/// FR-PLAT-COD-001..006, NFR-SEC-003/010).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CodesApiTests(SalahBahazadApiFactory factory)
{
    private const string CsvHeader =
        "Serial,Value,Status,Batch,Session,Created by,Created,Redeemed by,Redeemed at";

    [Fact]
    public async Task Generate_then_register_lists_codes_and_export_has_the_contract_columns()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 150m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant, staff.Id);

        // Generate (value omitted → defaults to the session price).
        var batch = await teacher.GenerateBatchAsync(session.Id, quantity: 3);
        batch.Quantity.Should().Be(3);
        batch.Value.Should().Be(150m);
        batch.SessionTitle.Should().Be(session.Title);
        batch.Label.Should().StartWith("CODES-");

        // The register shows them, joined to batch/session/creator.
        var codes = await teacher.ListBatchCodesAsync(batch.BatchId);
        codes.Should().HaveCount(3);
        codes.Should().OnlyContain(c => c.Status == "Active" && c.Value == 150m);
        codes.Should().OnlyContain(c => c.SessionTitle == session.Title && c.BatchLabel == batch.Label);
        codes.Should().OnlyContain(c => c.CreatedByName == staff.DisplayName);
        codes.Should().OnlyContain(c => c.Serial.StartsWith("SB-"));

        // The server CSV export carries the full contract column set + a real row.
        var export = await teacher.GetAsync($"/api/codes/export?batchId={batch.BatchId}");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await export.Content.ReadAsStringAsync();
        csv.Should().Contain(CsvHeader);
        csv.Should().Contain(codes[0].Serial);
    }

    [Fact]
    public async Task Codes_are_isolated_per_tenant()
    {
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();
        var (gradeA, _, specA) = await factory.SeedTaxonomyAsync(tenantA);
        var sessionA = await factory.SeedSessionWithContentAsync(tenantA, gradeA, specA);

        await factory.CreateClientFor(StaffRole.Teacher, tenantA).GenerateBatchAsync(sessionA.Id, quantity: 2);

        // Tenant B sees none of tenant A's codes.
        var clientB = factory.CreateClientFor(StaffRole.Teacher, tenantB);
        var page = await clientB.GetFromJsonAsync<PagedCodeResponse>("/api/codes", TestJson.Options);
        page!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Disable_enable_delete_round_trip()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var batch = await teacher.GenerateBatchAsync(session.Id, quantity: 1);
        var code = (await teacher.ListBatchCodesAsync(batch.BatchId)).Single();

        var disabled = await teacher.PostAsync($"/api/codes/{code.Id}/disable", null);
        disabled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await disabled.Content.ReadFromJsonAsync<CodeListItem>(TestJson.Options))!.Status.Should().Be("Inactive");

        var enabled = await teacher.PostAsync($"/api/codes/{code.Id}/enable", null);
        (await enabled.Content.ReadFromJsonAsync<CodeListItem>(TestJson.Options))!.Status.Should().Be("Active");

        (await teacher.DeleteAsync($"/api/codes/{code.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Soft-deleted → drops out of the register.
        var remaining = await teacher.ListBatchCodesAsync(batch.BatchId);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Default_deny_assistant_reads_but_cannot_generate_disable_or_delete()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var assistant = factory.CreateClientFor(StaffRole.Assistant, tenant);

        // Read is allowed for assistants.
        (await assistant.GetAsync("/api/codes")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Generate / disable / delete are Teacher-only → 403 (enforced before the handler runs).
        var generate = await assistant.PostAsJsonAsync(
            "/api/codes/batches", new GenerateCodesRequestBody(session.Id, null, 1), TestJson.Options);
        generate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await assistant.PostAsync($"/api/codes/{Guid.NewGuid()}/disable", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await assistant.DeleteAsync($"/api/codes/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_caller_is_unauthorized()
    {
        var anon = factory.CreateClient();
        (await anon.GetAsync("/api/codes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(1000)]
    public async Task List_accepts_a_large_page_size(int pageSize)
    {
        var tenant = await factory.SeedTenantAsync();
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        (await teacher.GetAsync($"/api/codes?pageSize={pageSize}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
