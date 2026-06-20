using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Assignment audit attribution (FR-PLAT-AUD-002/005): generation-on-enroll and auto-grading each leave exactly
/// one entry attributed to the <b>System</b> actor (not the enrolling/answering principal), while the
/// high-volume per-answer and behaviour writes leave <b>no</b> audit rows (they go to <c>assessment_events</c>).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AssignmentAuditTests(SalahBahazadApiFactory factory)
{
    private Task<List<AuditEntry>> AuditEntriesAsync(Guid tenant) =>
        factory.QueryDbAsync(db => db.AuditEntries
            .Where(a => a.TenantId == tenant)
            .OrderBy(a => a.Id)
            .ToListAsync());

    [Fact]
    public async Task Generation_and_grading_are_attributed_to_System_and_behaviour_writes_no_audit()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);

        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(serial);

        // Behaviour event (must never hit the audit log).
        var assignment = await studentClient.GetMyAssignmentAsync(session.Id);
        await studentClient.PostAsJsonAsync($"/api/me/assignments/{assignment.Id}/events",
            new RecordEventBody("Entered", null, DateTimeOffset.UtcNow, 1000), TestJson.Options);

        // Complete → auto-grade.
        await studentClient.CompleteAssignmentCorrectlyAsync(session.Id);

        var entries = await AuditEntriesAsync(tenant);

        var generated = entries.Should().ContainSingle(a => a.Action == "AssignmentGenerated").Subject;
        generated.ActorType.Should().Be("System");
        generated.ActorId.Should().BeNull();
        generated.EntityType.Should().Be(nameof(UserAssignment));

        var graded = entries.Should().ContainSingle(a => a.Action == "AssignmentGraded").Subject;
        graded.ActorType.Should().Be("System");
        graded.ActorId.Should().BeNull();
        graded.Summary.Should().Contain("100%");

        // The redeem itself is still attributed to the student (the System override is scoped to the assignment events).
        entries.Should().ContainSingle(a => a.Action == "CodeRedeemed")
            .Which.ActorType.Should().Be("Student");

        // Per-answer + behaviour writes leave no audit rows of their own.
        entries.Should().NotContain(a => a.EntityType == nameof(AssessmentEvent));
        entries.Should().NotContain(a => a.EntityType == nameof(AssignmentQuestion));
        entries.Should().NotContain(a => a.EntityType == nameof(AssignmentOption));
        entries.Should().NotContain(a => a.EntityType == nameof(UserAssignment) && a.Action == "Updated");
    }
}
