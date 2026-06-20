using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Admin attendance matrix + per-student breakdown (contract §B, FR-ADM-ATT-001/002/004) and assignment/behaviour
/// review (contract §C, FR-ADM-REV-001/003): the documented shapes, the audited CSV export, default-deny and
/// tenant isolation (NFR-SEC-010).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AttendanceReviewApiTests(SalahBahazadApiFactory factory)
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
    public async Task Session_matrix_and_student_breakdown_expose_the_assignment_score()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(serial);
        await studentClient.CompleteAssignmentCorrectlyAsync(session.Id); // 100%

        var matrix = await teacher.GetFromJsonAsync<PagedSessionAttendance>(
            $"/api/attendance/sessions/{session.Id}", TestJson.Options);
        var row = matrix!.Items.Should().ContainSingle(r => r.StudentId == student.Id).Subject;
        row.StudentName.Should().Be(student.FullName);
        row.AssignmentPercent.Should().Be(100);
        row.VideosTotal.Should().Be(2);          // seeded video count
        row.VideosWatched.Should().Be(0);        // 5C
        row.BestQuizPercent.Should().BeNull();   // 5B-2
        row.QuizAttemptCount.Should().Be(0);     // 5B-2

        var breakdown = await teacher.GetFromJsonAsync<PagedStudentAttendance>(
            $"/api/attendance/students/{student.Id}", TestJson.Options);
        breakdown!.Items.Should().ContainSingle(r =>
            r.SessionId == session.Id && r.SessionTitle == session.Title
            && r.AssignmentPercent == 100 && r.VideosTotal == 2);
    }

    [Fact]
    public async Task Refunded_enrollments_drop_out_of_the_cohort()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var enrollment = await studentClient.RedeemAsync(serial);

        await teacher.PostAsJsonAsync(
            $"/api/enrollments/{enrollment.Id}/refund", new RefundRequestBody("test"), TestJson.Options);

        var matrix = await teacher.GetFromJsonAsync<PagedSessionAttendance>(
            $"/api/attendance/sessions/{session.Id}", TestJson.Options);
        matrix!.Items.Should().NotContain(r => r.StudentId == student.Id);
    }

    [Fact]
    public async Task Review_shows_submitted_vs_correct_score_time_and_behaviour()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var enrollment = await studentClient.RedeemAsync(serial);

        var assignment = await studentClient.GetMyAssignmentAsync(session.Id);
        var q1 = assignment.Questions.First(q => q.Order == 1);
        var q2 = assignment.Questions.First(q => q.Order == 2);

        await studentClient.PostAsJsonAsync($"/api/me/assignments/{assignment.Id}/events",
            new RecordEventBody("Entered", null, DateTimeOffset.UtcNow, 4000), TestJson.Options);
        await studentClient.AnswerAsync(assignment.Id, q1.Id, q1.Options.Single(o => o.Text == "A").Id); // correct
        await studentClient.AnswerAsync(assignment.Id, q2.Id, q2.Options.Single(o => o.Text == "B").Id); // wrong

        var review = await teacher.GetFromJsonAsync<AssignmentReview>(
            $"/api/review/assignments/{enrollment.Id}", TestJson.Options);
        review!.StudentName.Should().Be(student.FullName);
        review.SessionTitle.Should().Be(session.Title);
        review.Status.Should().Be("Completed");
        review.QuestionCount.Should().Be(2);
        review.CorrectCount.Should().Be(1);
        review.ScoreMarks.Should().Be(1);
        review.MaxMarks.Should().Be(2);
        review.Percent.Should().Be(50);
        review.TimeSpentSeconds.Should().Be(4); // 4000ms accrued

        var rq1 = review.Questions.Single(q => q.Order == 1);
        rq1.IsCorrect.Should().BeTrue();
        rq1.SelectedOptionId.Should().NotBeNull();
        rq1.Options.Should().ContainSingle(o => o.IsCorrect);      // correctness shown to staff
        review.Questions.Single(q => q.Order == 2).IsCorrect.Should().BeFalse();

        var behaviour = await teacher.GetFromJsonAsync<List<BehaviourEvent>>(
            $"/api/review/assignments/{enrollment.Id}/behaviour", TestJson.Options);
        behaviour!.Should().Contain(e => e.Type == "Entered");
        behaviour.Should().Contain(e => e.Type == "Answered" && e.Label == "Answered Q1");
    }

    [Fact]
    public async Task Attendance_export_streams_csv_and_is_audited()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(serial);
        await studentClient.CompleteAssignmentCorrectlyAsync(session.Id); // 100%

        var export = await teacher.GetAsync($"/api/attendance/sessions/{session.Id}/export");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await export.Content.ReadAsStringAsync();
        csv.Should().Contain("Student,Videos watched,Videos total,Assignment %,Best quiz %,Quiz attempts");
        csv.Should().Contain(student.FullName);
        csv.Should().Contain("100");

        // A GET cannot reach the interceptor → audited explicitly, one row, attributed to the staff actor.
        var exported = await factory.QueryDbAsync(db => db.AuditEntries
            .Where(a => a.TenantId == tenant && a.Action == "AttendanceExported")
            .ToListAsync());
        exported.Should().ContainSingle();
        exported[0].EntityType.Should().Be("Session");
        exported[0].ActorType.Should().Be("Staff");
    }

    [Fact]
    public async Task Attendance_and_review_are_staff_only_and_tenant_isolated()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1);

        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        (await studentClient.GetAsync($"/api/attendance/sessions/{session.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.GetAsync($"/api/review/assignments/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync($"/api/attendance/sessions/{session.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Tenant B staff cannot see tenant A's session attendance (the session is not in B's tenant) → 404.
        var tenantB = await factory.SeedTenantAsync();
        (await factory.CreateClientFor(StaffRole.Teacher, tenantB).GetAsync($"/api/attendance/sessions/{session.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
