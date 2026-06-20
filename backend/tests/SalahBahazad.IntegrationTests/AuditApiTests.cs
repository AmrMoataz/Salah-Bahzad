using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The activity-log feed (contract #1, FR-ADM-AUD-001..003, scrActivity): tenant isolation (NFR-SEC-010),
/// Assistant-vs-Teacher sensitive scoping (FR-ADM-AUD-003), the actor/category/period + entity filters, the
/// resolved actor/target/category, paging/order, and default-deny (NFR-SEC-003).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuditApiTests(SalahBahazadApiFactory factory)
{
    private static async Task<PagedAuditResponse> FeedAsync(HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<PagedAuditResponse>($"/api/audit{query}", TestJson.Options))!;

    [Fact]
    public async Task Anonymous_is_unauthorized_and_non_staff_is_forbidden()
    {
        var tenant = await factory.SeedTenantAsync();

        (await factory.CreateClient().GetAsync("/api/audit"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A Student-role token authenticates but holds no AuditRead → 403 (default-deny, contract §0).
        (await factory.CreateClientForStudent(tenant, Guid.NewGuid()).GetAsync("/api/audit"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Feed_is_isolated_per_tenant()
    {
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();

        await factory.SeedAuditAsync(tenantA, "StudentApproved");
        await factory.SeedAuditAsync(tenantA, "CodeBatchGenerated", entityType: "CodeBatch");
        var onlyB = await factory.SeedAuditAsync(tenantB, "SessionPublished", entityType: "Session");

        // Tenant B's token sees only B's row — AuditEntry is NOT globally tenant-filtered, so the explicit
        // Where(a => a.TenantId == tenant) is the only thing standing between tenants (NFR-SEC-010).
        var page = await FeedAsync(factory.CreateClientFor(StaffRole.Teacher, tenantB));

        page.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(onlyB.Id);
    }

    [Fact]
    public async Task Assistant_feed_hides_sensitive_rows_that_teacher_sees()
    {
        var tenant = await factory.SeedTenantAsync();
        await factory.SeedAuditAsync(tenant, "StudentApproved");
        await factory.SeedAuditAsync(tenant, "StudentIdImageViewed"); // the sole "who-read-what" action (§4)

        var teacherFeed = await FeedAsync(factory.CreateClientFor(StaffRole.Teacher, tenant));
        teacherFeed.Items.Select(i => i.Action)
            .Should().Contain(["StudentApproved", "StudentIdImageViewed"]);

        var assistantFeed = await FeedAsync(factory.CreateClientFor(StaffRole.Assistant, tenant));
        assistantFeed.Items.Select(i => i.Action).Should().Contain("StudentApproved");
        assistantFeed.Items.Select(i => i.Action).Should().NotContain("StudentIdImageViewed");
        assistantFeed.Total.Should().Be(1); // excluded from the count, not just the page
    }

    [Fact]
    public async Task Feed_resolves_actor_role_target_label_and_category_newest_first()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId);
        var session = await factory.SeedSessionAsync(tenant, gradeId, specId, title: "Newton's Laws");

        var now = DateTimeOffset.UtcNow;
        await factory.SeedAuditAsync(tenant, "SessionPublished", "Session", session.Id,
            actorId: staff.Id, occurredAtUtc: now.AddMinutes(-5));
        await factory.SeedAuditAsync(tenant, "StudentApproved", "Student", student.Id,
            actorId: staff.Id, occurredAtUtc: now.AddMinutes(-1));

        var feed = await FeedAsync(factory.CreateClientFor(StaffRole.Teacher, tenant));

        feed.Items.Select(i => i.Action).Should().ContainInOrder("StudentApproved", "SessionPublished");

        var approved = feed.Items.Single(i => i.Action == "StudentApproved");
        approved.ActorType.Should().Be("Staff");
        approved.ActorRole.Should().Be("Teacher");
        approved.ActorName.Should().Be(staff.DisplayName);
        approved.Category.Should().Be("approval");
        approved.TargetType.Should().Be("Student");
        approved.TargetId.Should().Be(student.Id);
        approved.TargetLabel.Should().Be(student.FullName);

        var published = feed.Items.Single(i => i.Action == "SessionPublished");
        published.Category.Should().Be("session");
        published.TargetLabel.Should().Be("Newton's Laws");
    }

    [Fact]
    public async Task Feed_filters_by_actor_type_category_entity_and_period()
    {
        var tenant = await factory.SeedTenantAsync();
        var staff1 = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var staff2 = await factory.SeedStaffAsync(tenant, StaffRole.Assistant);
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await factory.SeedAuditAsync(tenant, "CodeBatchGenerated", "CodeBatch",
            actorId: staff1.Id, occurredAtUtc: now.AddDays(-1));
        await factory.SeedAuditAsync(tenant, "SessionPublished", "Session", sessionId,
            actorId: staff1.Id, occurredAtUtc: now.AddDays(-1));
        await factory.SeedAuditAsync(tenant, "StudentApproved", "Student", studentId,
            actorId: staff2.Id, occurredAtUtc: now.AddDays(-1));
        // A student-actor code redemption, older than 7 days.
        await factory.SeedAuditAsync(tenant, "CodeRedeemed", "Code",
            actorType: "Student", actorId: studentId, actorRole: "Student", occurredAtUtc: now.AddDays(-40));

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        (await FeedAsync(teacher, $"?actorId={staff1.Id}")).Items
            .Should().HaveCount(2).And.OnlyContain(i => i.ActorType == "Staff");

        (await FeedAsync(teacher, "?actorType=Student")).Items
            .Should().ContainSingle().Which.Action.Should().Be("CodeRedeemed");

        (await FeedAsync(teacher, "?category=code")).Items.Select(i => i.Action)
            .Should().BeEquivalentTo("CodeBatchGenerated", "CodeRedeemed");

        (await FeedAsync(teacher, "?category=session")).Items
            .Should().ContainSingle().Which.Action.Should().Be("SessionPublished");

        (await FeedAsync(teacher, $"?studentId={studentId}")).Items
            .Should().ContainSingle().Which.Action.Should().Be("StudentApproved");

        (await FeedAsync(teacher, $"?sessionId={sessionId}")).Items
            .Should().ContainSingle().Which.Action.Should().Be("SessionPublished");

        (await FeedAsync(teacher, "?entityType=Code")).Items
            .Should().ContainSingle().Which.Action.Should().Be("CodeRedeemed");

        // period=7d excludes the 40-day-old redemption; 90d includes it.
        (await FeedAsync(teacher, "?period=7d")).Items
            .Should().HaveCount(3).And.NotContain(i => i.Action == "CodeRedeemed");
        (await FeedAsync(teacher, "?period=90d")).Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task Feed_pages_and_reports_total()
    {
        var tenant = await factory.SeedTenantAsync();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
            await factory.SeedAuditAsync(tenant, "StudentApproved", occurredAtUtc: now.AddMinutes(-i));

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var page1 = await FeedAsync(teacher, "?page=1&pageSize=2");
        page1.Total.Should().Be(3);
        page1.Items.Should().HaveCount(2);

        var page2 = await FeedAsync(teacher, "?page=2&pageSize=2");
        page2.Total.Should().Be(3);
        page2.Items.Should().HaveCount(1);
    }
}
