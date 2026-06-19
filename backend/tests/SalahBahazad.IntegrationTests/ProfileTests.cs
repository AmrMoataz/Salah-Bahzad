using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Self-service profile endpoints (FR-ADM-SET-001): a signed-in staff member reads and updates their
/// own display name; the change is audited; anonymous callers are rejected.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProfileTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Anonymous_profile_request_is_rejected_with_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Staff_can_read_and_update_their_own_profile_and_it_is_audited()
    {
        var tenantId = await factory.SeedTenantAsync();
        var staff = await factory.SeedStaffAsync(tenantId, StaffRole.Assistant);
        var client = factory.CreateClientFor(StaffRole.Assistant, tenantId, staff.Id);

        // Read own profile.
        var profile = await client.GetFromJsonAsync<StaffResponse>("/api/profile", TestJson.Options);
        profile.Should().NotBeNull();
        profile!.Email.Should().Be(staff.Email);

        // Update display name.
        var update = await client.PutAsJsonAsync("/api/profile", new { displayName = "Mostafa Kamel" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<StaffResponse>(TestJson.Options);
        updated!.DisplayName.Should().Be("Mostafa Kamel");
        updated.Email.Should().Be(staff.Email); // email is the Firebase identity — unchanged

        // The change persisted.
        var after = await client.GetFromJsonAsync<StaffResponse>("/api/profile", TestJson.Options);
        after!.DisplayName.Should().Be("Mostafa Kamel");

        // ...and was audited.
        var audit = await factory.LatestAuditAsync(tenantId, "Staff", "Updated");
        audit.Should().NotBeNull();
        audit!.ActorId.Should().Be(staff.Id);
    }

    [Fact]
    public async Task Empty_display_name_is_rejected_with_400()
    {
        var tenantId = await factory.SeedTenantAsync();
        var staff = await factory.SeedStaffAsync(tenantId, StaffRole.Teacher);
        var client = factory.CreateClientFor(StaffRole.Teacher, tenantId, staff.Id);

        var response = await client.PutAsJsonAsync("/api/profile", new { displayName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
