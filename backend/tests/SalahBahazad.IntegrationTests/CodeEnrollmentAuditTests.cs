using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Audit is first-class (FR-PLAT-AUD-002): generate, export, disable, enable, delete, redeem, unlock and
/// refund each leave exactly one semantic entry — including the read-only CSV export (written explicitly,
/// proving the interceptor-miss is handled) — attributed to the right actor, with the high-volume child rows
/// (codes, counters, payments, attendance) suppressed, and the hash chain intact (FR-PLAT-AUD-002/005).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CodeEnrollmentAuditTests(SalahBahazadApiFactory factory)
{
    private Task<List<AuditEntry>> AuditEntriesAsync(Guid tenant) =>
        factory.QueryDbAsync(db => db.AuditEntries
            .Where(a => a.TenantId == tenant)
            .OrderBy(a => a.Id)
            .ToListAsync());

    [Fact]
    public async Task Code_lifecycle_and_export_each_leave_one_staff_attributed_entry()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant, staff.Id);

        var batch = await teacher.GenerateBatchAsync(session.Id, quantity: 5);
        var code = (await teacher.ListBatchCodesAsync(batch.BatchId)).First();
        await teacher.PostAsync($"/api/codes/{code.Id}/disable", null);
        await teacher.PostAsync($"/api/codes/{code.Id}/enable", null);
        await teacher.GetAsync($"/api/codes/export?batchId={batch.BatchId}");
        await teacher.DeleteAsync($"/api/codes/{code.Id}");

        var entries = await AuditEntriesAsync(tenant);

        entries.Count(a => a.Action == "CodeBatchGenerated").Should().Be(1);
        entries.Count(a => a.Action == "CodeDisabled").Should().Be(1);
        entries.Count(a => a.Action == "CodeEnabled").Should().Be(1);
        entries.Count(a => a.Action == "CodeDeleted").Should().Be(1);
        entries.Count(a => a.Action == "CodesExported").Should().Be(1); // GET export audited explicitly

        // The 5 minted codes do NOT each add a row — generate stays one entry (IAuditViaEventOnly).
        entries.Should().NotContain(a => a.EntityType == "Code" && a.Action == "Created");

        // Every code/export entry is attributed to the acting staff member, with a semantic summary.
        entries.Where(a => a.Action.StartsWith("Code"))
            .Should().OnlyContain(a => a.ActorType == "Staff" && a.ActorId == staff.Id);
        entries.Single(a => a.Action == "CodeBatchGenerated").Summary.Should().Contain("Generated");
    }

    [Fact]
    public async Task Redeem_is_attributed_to_the_student_and_suppresses_child_rows()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m, videoCount: 2);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);

        await factory.CreateClientForStudent(tenant, student.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options);

        var entries = await AuditEntriesAsync(tenant);

        var redeemed = entries.Should().ContainSingle(a => a.Action == "CodeRedeemed").Subject;
        redeemed.ActorType.Should().Be("Student");
        redeemed.ActorId.Should().Be(student.Id);

        var created = entries.Should().ContainSingle(a => a.Action == "EnrollmentCreated").Subject;
        created.ActorType.Should().Be("Student");
        created.ActorId.Should().Be(student.Id);

        // The provisioning children leave no generic rows of their own.
        entries.Should().NotContain(a =>
            a.Action == "Created" &&
            (a.EntityType == "PaymentTransaction" || a.EntityType == "EnrollmentVideoAccess" || a.EntityType == "Attendance"));
    }

    [Fact]
    public async Task Unlock_and_refund_are_attributed_to_staff()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var staff = await factory.SeedStaffAsync(tenant, StaffRole.Teacher);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant, staff.Id);

        var enrollment = (await (await teacher.PostAsJsonAsync(
                $"/api/sessions/{session.Id}/unlock", new UnlockRequestBody(student.Id), TestJson.Options))
            .Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!;

        await teacher.PostAsJsonAsync(
            $"/api/enrollments/{enrollment.Id}/refund", new RefundRequestBody("test"), TestJson.Options);

        var entries = await AuditEntriesAsync(tenant);

        entries.Should().ContainSingle(a => a.Action == "EnrollmentCreated")
            .Which.ActorType.Should().Be("Staff");
        entries.Single(a => a.Action == "EnrollmentCreated").ActorId.Should().Be(staff.Id);
        entries.Should().ContainSingle(a => a.Action == "EnrollmentRefunded")
            .Which.ActorType.Should().Be("Staff");
    }

    [Fact]
    public async Task Audit_hash_chain_links_across_staff_and_student_actions()
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

        var entries = await AuditEntriesAsync(tenant);
        entries.Should().HaveCountGreaterThan(1);
        entries.Should().OnlyContain(a => !string.IsNullOrEmpty(a.Hash));

        // Verify integrity by following PrevHash → Hash links (not Id order: two entries written in one
        // SaveChanges can take same-millisecond UUIDv7 ids in either order). Exactly one genesis entry
        // (null PrevHash), and the chain must link through every entry with no break or fork.
        entries.Where(a => a.PrevHash is null).Should().ContainSingle();

        var byPrevHash = entries.Where(a => a.PrevHash is not null).ToDictionary(a => a.PrevHash!);
        var current = entries.Single(a => a.PrevHash is null);
        var visited = 1;
        while (byPrevHash.TryGetValue(current.Hash!, out var next))
        {
            current = next;
            visited++;
        }

        visited.Should().Be(entries.Count); // every entry is reachable through the chain
    }
}
