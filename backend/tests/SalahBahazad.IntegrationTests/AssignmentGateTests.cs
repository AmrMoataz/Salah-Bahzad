using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The prerequisite-assignment gate (contract §D, FR-PLAT-ENR-007): a session with a prerequisite cannot be
/// enrolled until the student has a <c>Completed</c> assignment for it (→409); a prerequisite with no question
/// bank passes vacuously. Closes the Phase-4 deferral, on both the redeem (#12) and unlock (#9) paths.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AssignmentGateTests(SalahBahazadApiFactory factory)
{
    private async Task<(Guid Tenant, Guid GradeId, Guid SpecId, StudentEntity Student)> SetupAsync()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        return (tenant, gradeId, specId, student);
    }

    [Fact]
    public async Task Redeem_is_blocked_until_the_prerequisite_assignment_is_completed()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var prereq = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1, price: 100m);
        var dependent = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1, price: 100m);
        await factory.SetSessionPrerequisiteAsync(dependent.Id, prereq.Id);

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var dependentSerial = await teacher.GenerateOneSerialAsync(dependent.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        // Prerequisite not completed → 409 (the code stays Active — the gate throws before redeem).
        (await studentClient.PostAsJsonAsync(
                "/api/enrollments/redeem", new RedeemRequestBody(dependentSerial), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Enroll in + complete the prerequisite assignment.
        var prereqSerial = await teacher.GenerateOneSerialAsync(prereq.Id);
        await studentClient.RedeemAsync(prereqSerial);
        await studentClient.CompleteAssignmentCorrectlyAsync(prereq.Id);

        // The same dependent code now redeems successfully.
        (await studentClient.PostAsJsonAsync(
                "/api/enrollments/redeem", new RedeemRequestBody(dependentSerial), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Redeem_passes_vacuously_when_the_prerequisite_has_no_questions()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        // Prerequisite with NO question bank → nothing to complete.
        var prereq = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var dependent = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        await factory.SetSessionPrerequisiteAsync(dependent.Id, prereq.Id);

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(dependent.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        (await studentClient.PostAsJsonAsync(
                "/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Unlock_is_also_gated_by_the_prerequisite()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var prereq = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var dependent = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        await factory.SetSessionPrerequisiteAsync(dependent.Id, prereq.Id);

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        (await teacher.PostAsJsonAsync(
                $"/api/sessions/{dependent.Id}/unlock", new UnlockRequestBody(student.Id), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
