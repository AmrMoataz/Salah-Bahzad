using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The open-book assignment engine (contract §A, FR-PLAT-ASG-001..006), driven with a student JWT exactly like
/// the Phase-4 redeem path: generate-on-enroll, answer, auto-grade → attendance, behaviour + time, resume, plus
/// IDOR and default-deny.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AssignmentEngineTests(SalahBahazadApiFactory factory)
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
    public async Task Enroll_generates_snapshot_then_answers_autograde_and_write_attendance()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        await studentClient.RedeemAsync(serial);

        // The enrol side-effect generated a 2-question snapshot — in progress, no answers yet.
        var assignment = await studentClient.GetMyAssignmentAsync(session.Id);
        assignment.SessionId.Should().Be(session.Id);
        assignment.Status.Should().Be("InProgress");
        assignment.TimeSpentSeconds.Should().Be(0);
        assignment.Questions.Should().HaveCount(2);
        assignment.Questions.Select(q => q.Order).Should().Equal(1, 2);
        assignment.Questions.Should().OnlyContain(q => q.Options.Count == 2 && q.SelectedOptionId == null);

        var q1 = assignment.Questions.First(q => q.Order == 1);
        var q2 = assignment.Questions.First(q => q.Order == 2);

        // First answer (correct) — still in progress.
        var p1 = await (await studentClient.AnswerAsync(assignment.Id, q1.Id, q1.Options.Single(o => o.Text == "A").Id))
            .Content.ReadFromJsonAsync<AssignmentProgress>(TestJson.Options);
        p1!.AnsweredCount.Should().Be(1);
        p1.QuestionCount.Should().Be(2);
        p1.Status.Should().Be("InProgress");

        // Last answer (wrong) — completes; 1/2 marks ⇒ 50%.
        var last = await studentClient.AnswerAsync(assignment.Id, q2.Id, q2.Options.Single(o => o.Text == "B").Id);
        last.StatusCode.Should().Be(HttpStatusCode.OK);
        (await last.Content.ReadFromJsonAsync<AssignmentProgress>(TestJson.Options))!.Status.Should().Be("Completed");

        // Auto-grade wrote Attendance.AssignmentScore (percent).
        var score = await factory.QueryDbAsync(db => db.Attendances.IgnoreQueryFilters()
            .Where(a => a.StudentId == student.Id && a.SessionId == session.Id)
            .Select(a => a.AssignmentScore)
            .FirstAsync());
        score.Should().Be(50);

        // Re-GET resumes the saved answers + status.
        var resumed = await studentClient.GetMyAssignmentAsync(session.Id);
        resumed.Status.Should().Be("Completed");
        resumed.Questions.First(q => q.Order == 1).SelectedOptionId.Should().NotBeNull();
        resumed.Questions.First(q => q.Order == 2).SelectedOptionId.Should().NotBeNull();
    }

    [Fact]
    public async Task Answering_after_completion_is_409()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(serial);

        var assignment = await studentClient.GetMyAssignmentAsync(session.Id);
        var q = assignment.Questions.Single();
        (await studentClient.AnswerAsync(assignment.Id, q.Id, q.Options.Single(o => o.Text == "A").Id))
            .StatusCode.Should().Be(HttpStatusCode.OK); // completes

        (await studentClient.AnswerAsync(assignment.Id, q.Id, q.Options.Single(o => o.Text == "B").Id))
            .StatusCode.Should().Be(HttpStatusCode.Conflict); // immutable result
    }

    [Fact]
    public async Task Student_assignment_shape_never_exposes_isCorrect()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(serial);

        var raw = await studentClient.GetStringAsync($"/api/me/assignments/by-session/{session.Id}");
        raw.Should().NotContainEquivalentOf("isCorrect");
    }

    [Fact]
    public async Task Behaviour_events_are_recorded_and_time_accrues()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(serial);

        var assignment = await studentClient.GetMyAssignmentAsync(session.Id);
        var now = DateTimeOffset.UtcNow;

        (await studentClient.PostAsJsonAsync($"/api/me/assignments/{assignment.Id}/events",
            new RecordEventBody("Entered", null, now, 5000), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await studentClient.PostAsJsonAsync($"/api/me/assignments/{assignment.Id}/events",
            new RecordEventBody("Navigated", 1, now.AddSeconds(5), 3000), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var eventCount = await factory.QueryDbAsync(db => db.AssessmentEvents.IgnoreQueryFilters()
            .CountAsync(e => e.UserAssignmentId == assignment.Id));
        eventCount.Should().Be(2);

        var resumed = await studentClient.GetMyAssignmentAsync(session.Id);
        resumed.TimeSpentSeconds.Should().Be(8); // 5000ms + 3000ms ⇒ 8s
    }

    [Fact]
    public async Task Posting_an_Answered_event_to_the_behaviour_endpoint_is_400()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(serial);
        var assignment = await studentClient.GetMyAssignmentAsync(session.Id);

        (await studentClient.PostAsJsonAsync($"/api/me/assignments/{assignment.Id}/events",
            new RecordEventBody("Answered", 1, DateTimeOffset.UtcNow, null), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_student_cannot_read_or_answer_another_students_assignment()
    {
        var (tenant, gradeId, specId, studentA) = await SetupAsync();
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var studentB = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);

        var clientA = factory.CreateClientForStudent(tenant, studentA.Id);
        await clientA.RedeemAsync(serial);
        var aAssignment = await clientA.GetMyAssignmentAsync(session.Id);
        var aq = aAssignment.Questions.Single();

        var clientB = factory.CreateClientForStudent(tenant, studentB.Id);
        // B has no enrollment for this session → GET 404.
        (await clientB.GetAsync($"/api/me/assignments/by-session/{session.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        // B cannot answer A's assignment (GUID-in-URL is not authorization, NFR-SEC-007) → 403.
        (await clientB.AnswerAsync(aAssignment.Id, aq.Id, aq.Options.First().Id))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Engine_is_student_only_default_deny()
    {
        var (tenant, gradeId, specId, _) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);
        var path = $"/api/me/assignments/by-session/{session.Id}";

        (await factory.CreateClient().GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClientFor(StaffRole.Teacher, tenant).GetAsync(path))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
