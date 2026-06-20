using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The operational dashboard (contract #2, FR-ADM-DASH-001..003, scrDashboard): the 4 KPIs + codes used/active
/// + revenue-by-code, the zero-filled enrollments-by-day series (period-scoped, KPIs are not), the tenant-scoped
/// non-sensitive recent activity (NFR-SEC-010), and default-deny (NFR-SEC-003).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardApiTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Anonymous_is_unauthorized_and_non_staff_is_forbidden()
    {
        var tenant = await factory.SeedTenantAsync();

        (await factory.CreateClient().GetAsync("/api/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClientForStudent(tenant, Guid.NewGuid()).GetAsync("/api/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_reports_kpis_revenue_series_and_recent_activity()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);

        var redeemer = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var unlockee = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Pending);
        await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Pending);
        await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Rejected);

        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant, staff.Id);

        // 3 codes left Active + 1 minted then redeemed (→ Used, revenue 100, enrollment #1 today).
        await teacher.GenerateBatchAsync(session.Id, quantity: 3);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        await factory.CreateClientForStudent(tenant, redeemer.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options);

        // Staff unlock → enrollment #2 today (amount 0, no code → no revenue).
        await teacher.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/unlock", new UnlockRequestBody(unlockee.Id), TestJson.Options);

        var dash = (await teacher.GetFromJsonAsync<DashboardResponse>("/api/dashboard", TestJson.Options))!;

        dash.PendingApprovals.Should().Be(2);
        dash.ActiveStudents.Should().Be(2);
        dash.CodesUsed.Should().Be(1);
        dash.CodesActive.Should().Be(3);
        dash.RevenueFromCodes.Should().Be(100m);

        // Two enrollments today; the series is daily and zero-filled across the whole window.
        dash.EnrollmentsTotal.Should().Be(2);
        dash.EnrollmentsByDay.Sum(d => d.Count).Should().Be(2);
        dash.EnrollmentsByDay.Count.Should().BeGreaterThan(7);
        dash.PeriodFrom.Should().BeBefore(dash.PeriodTo);

        dash.RecentActivity.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(7);
        dash.RecentActivity.Should().NotContain(i => i.Action == "StudentIdImageViewed");
        dash.RecentActivity.Select(i => i.Action).Should().Contain("CodeRedeemed");
        dash.RecentActivity.Should().OnlyContain(i => !string.IsNullOrEmpty(i.Category));
    }

    [Fact]
    public async Task Dashboard_period_scopes_only_the_enrollments_series()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        await factory.CreateClientForStudent(tenant, student.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options);

        // A window entirely in the past excludes today's enrollment but NOT the point-in-time KPIs.
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-60).ToString("o"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-40).ToString("o"));
        var dash = (await teacher.GetFromJsonAsync<DashboardResponse>(
            $"/api/dashboard?from={from}&to={to}", TestJson.Options))!;

        dash.EnrollmentsTotal.Should().Be(0);
        dash.EnrollmentsByDay.Should().OnlyContain(d => d.Count == 0);
        dash.CodesUsed.Should().Be(1);          // KPI is point-in-time, not period-scoped
        dash.RevenueFromCodes.Should().Be(100m);
    }

    [Fact]
    public async Task Dashboard_recent_activity_is_capped_tenant_scoped_and_non_sensitive()
    {
        var tenantA = await factory.SeedTenantAsync();
        var tenantB = await factory.SeedTenantAsync();
        var now = DateTimeOffset.UtcNow;

        var aIds = new List<Guid>();
        for (var i = 0; i < 8; i++)
            aIds.Add((await factory.SeedAuditAsync(tenantA, "StudentApproved", occurredAtUtc: now.AddMinutes(-10 - i))).Id);
        // The newest entry in A is sensitive — it must still be excluded despite being most recent.
        await factory.SeedAuditAsync(tenantA, "StudentIdImageViewed", occurredAtUtc: now);
        // Tenant B's rows are NEWER than A's — they would dominate the feed if the tenant filter leaked.
        for (var i = 0; i < 5; i++)
            await factory.SeedAuditAsync(tenantB, "SessionPublished", "Session", occurredAtUtc: now.AddMinutes(1 + i));

        var dash = (await factory.CreateClientFor(StaffRole.Teacher, tenantA)
            .GetFromJsonAsync<DashboardResponse>("/api/dashboard", TestJson.Options))!;

        dash.RecentActivity.Should().HaveCount(7);                            // capped at 7
        dash.RecentActivity.Should().OnlyContain(i => aIds.Contains(i.Id));   // tenant A only, never B
        dash.RecentActivity.Should().NotContain(i => i.Action == "StudentIdImageViewed"); // sensitive excluded
    }
}
