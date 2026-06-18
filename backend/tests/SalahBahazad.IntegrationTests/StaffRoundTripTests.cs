using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Create → list round trip, plus proof that the action is audited (FR-ADM-STAFF-004) and that
/// the role enum is serialized as its name, not an integer (Part A3).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StaffRoundTripTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Create_appears_in_list_and_writes_an_audit_entry()
    {
        var tenantId = await factory.SeedTenantAsync();
        var teacherId = Guid.NewGuid();
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId, teacherId);

        var email = $"created-{Guid.NewGuid():N}@x.com";
        var create = await client.PostAsJsonAsync("/api/staff",
            new { displayName = "Hossam Fathy", email, role = "Assistant" });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<StaffResponse>(TestJson.Options);
        created.Should().NotBeNull();
        created!.Role.Should().Be("Assistant"); // enum serialized as name (A3)
        created.Email.Should().Be(email);

        var page = await client.GetFromJsonAsync<PagedStaffResponse>("/api/staff", TestJson.Options);
        page!.Items.Should().Contain(i => i.Email == email);

        var audit = await factory.LatestStaffAuditAsync(tenantId, "Created");
        audit.Should().NotBeNull();
        audit!.ActorId.Should().Be(teacherId);
        audit.ActorType.Should().Be("Staff");
    }
}
