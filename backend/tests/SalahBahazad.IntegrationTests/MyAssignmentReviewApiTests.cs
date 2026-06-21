using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The student answer-key review — <c>GET /api/me/assignments/{assignmentId}/review</c> (contract §B,
/// FR-STU-ASG-007/FR-PLAT-ASG-008): the <b>only</b> student surface that exposes option correctness, gated to the
/// caller's own <c>Completed</c> assignment. Proves the 200 answer-key + score, the 403
/// <c>assignment_in_progress</c> gate, the 404 IDOR/cross-tenant boundary (NFR-SEC-007/010), student-only
/// default-deny, the read is not audited (§E), and that the student/staff <c>isCorrect</c> split holds.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyAssignmentReviewApiTests(SalahBahazadApiFactory factory)
{
    private async Task<(Guid Tenant, Guid GradeId, Guid SpecId, StudentEntity Student)> SetupAsync()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        return (tenant, gradeId, specId, student);
    }

    /// <summary>Enrols the student and answers Q1 correct ("A") + Q2 wrong ("B") so the 2-question assignment
    /// auto-grades to Completed at 50% (1/2 marks) — the mixed score the review must report. Returns the
    /// assignment id and the two picked option ids (for echo assertions).</summary>
    private async Task<(Guid AssignmentId, Guid Q1AOptionId, Guid Q2BOptionId)> CompleteMixedAsync(
        HttpClient studentClient, HttpClient teacher, Guid sessionId)
    {
        var serial = await teacher.GenerateOneSerialAsync(sessionId);
        await studentClient.RedeemAsync(serial);
        var assignment = await studentClient.GetMyAssignmentAsync(sessionId);
        var q1 = assignment.Questions.First(q => q.Order == 1);
        var q2 = assignment.Questions.First(q => q.Order == 2);
        var q1A = q1.Options.Single(o => o.Text == "A").Id;
        var q2B = q2.Options.Single(o => o.Text == "B").Id;
        (await studentClient.AnswerAsync(assignment.Id, q1.Id, q1A)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await studentClient.AnswerAsync(assignment.Id, q2.Id, q2B)).StatusCode.Should().Be(HttpStatusCode.OK);
        return (assignment.Id, q1A, q2B);
    }

    [Fact]
    public async Task Review_returns_the_answer_key_and_score_for_the_callers_completed_assignment()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var start = DateTimeOffset.UtcNow;
        var (assignmentId, q1A, q2B) = await CompleteMixedAsync(studentClient, teacher, session.Id);

        var review = await studentClient.GetFromJsonAsync<StudentAssignmentReviewResponse>(
            $"/api/me/assignments/{assignmentId}/review", TestJson.Options);

        review!.Id.Should().Be(assignmentId);              // echoes the route param
        review.SessionId.Should().Be(session.Id);
        review.SessionTitle.Should().Be(session.Title);
        review.Status.Should().Be("Completed");
        review.QuestionCount.Should().Be(2);
        review.CorrectCount.Should().Be(1);
        review.ScoreMarks.Should().Be(1);
        review.MaxMarks.Should().Be(2);
        review.Percent.Should().Be(50);                    // round(100 × 1 / 2)
        review.CompletedAtUtc.Should().BeOnOrAfter(start);

        // Questions ordered by Order; every question's options ordered, with the answer key (isCorrect) exposed.
        review.Questions.Select(q => q.Order).Should().Equal(1, 2);
        foreach (var q in review.Questions)
        {
            q.Options.Select(o => o.Order).Should().BeInAscendingOrder();
            q.Options.Should().ContainSingle(o => o.IsCorrect).Which.Text.Should().Be("A");
            q.Mark.Should().Be(1);
        }

        var rq1 = review.Questions.Single(q => q.Order == 1);
        rq1.IsCorrect.Should().BeTrue();
        rq1.SelectedOptionId.Should().Be(q1A);             // the student's pick is echoed

        var rq2 = review.Questions.Single(q => q.Order == 2);
        rq2.IsCorrect.Should().BeFalse();
        rq2.SelectedOptionId.Should().Be(q2B);
    }

    [Fact]
    public async Task In_progress_assignment_is_403_with_assignment_in_progress_reason()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(await teacher.GenerateOneSerialAsync(session.Id));

        // Answer only one of two questions → still InProgress → the key stays hidden (never revealed pre-submit).
        var assignment = await studentClient.GetMyAssignmentAsync(session.Id);
        var q1 = assignment.Questions.First(q => q.Order == 1);
        await studentClient.AnswerAsync(assignment.Id, q1.Id, q1.Options.Single(o => o.Text == "A").Id);

        var response = await studentClient.GetAsync($"/api/me/assignments/{assignment.Id}/review");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be("assignment_in_progress");
        problem.Detail.Should().Be("Finish the assignment to see your answers and score.");
    }

    [Fact]
    public async Task Review_is_404_for_an_unknown_or_another_students_assignment()
    {
        var (tenant, gradeId, specId, studentA) = await SetupAsync();
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var studentB = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var clientA = factory.CreateClientForStudent(tenant, studentA.Id);
        var (assignmentId, _, _) = await CompleteMixedAsync(clientA, teacher, session.Id);

        // An unknown id → opaque 404.
        (await clientA.GetAsync($"/api/me/assignments/{Guid.NewGuid()}/review"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Another student (same tenant) cannot read A's completed assignment — a GUID in the URL is not
        // authorization (NFR-SEC-007) → 404, never A's data.
        var clientB = factory.CreateClientForStudent(tenant, studentB.Id);
        (await clientB.GetAsync($"/api/me/assignments/{assignmentId}/review"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Review_is_404_across_tenants()
    {
        var (tenantA, gradeIdA, specIdA, studentA) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenantA, gradeIdA, specIdA, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenantA);
        var clientA = factory.CreateClientForStudent(tenantA, studentA.Id);
        var (assignmentId, _, _) = await CompleteMixedAsync(clientA, teacher, session.Id);

        // A student in another tenant cannot see tenant A's assignment (the global query filter) → 404 (NFR-SEC-010).
        var (tenantB, _, _, studentB) = await SetupAsync();
        var clientB = factory.CreateClientForStudent(tenantB, studentB.Id);
        (await clientB.GetAsync($"/api/me/assignments/{assignmentId}/review"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Review_is_student_only_default_deny()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var (assignmentId, _, _) = await CompleteMixedAsync(studentClient, teacher, session.Id);
        var path = $"/api/me/assignments/{assignmentId}/review";

        (await factory.CreateClient().GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized); // anon
        (await teacher.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);                    // staff
    }

    [Fact]
    public async Task Reading_the_review_is_not_audited()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var (assignmentId, _, _) = await CompleteMixedAsync(studentClient, teacher, session.Id);

        var before = await factory.QueryDbAsync(db => db.AuditEntries.CountAsync(a => a.TenantId == tenant));
        (await studentClient.GetAsync($"/api/me/assignments/{assignmentId}/review"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await factory.QueryDbAsync(db => db.AuditEntries.CountAsync(a => a.TenantId == tenant));

        after.Should().Be(before); // a pure read of the caller's own homework writes no audit row (§E)
    }

    [Fact]
    public async Task Only_the_review_exposes_isCorrect_not_the_runner_shape()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var (assignmentId, _, _) = await CompleteMixedAsync(studentClient, teacher, session.Id);

        // The runner shape never carries correctness (the 5B-1 invariant)...
        (await studentClient.GetStringAsync($"/api/me/assignments/by-session/{session.Id}"))
            .Should().NotContainEquivalentOf("isCorrect");
        // ...but the review is the one surface that does (post-completion only).
        (await studentClient.GetStringAsync($"/api/me/assignments/{assignmentId}/review"))
            .Should().ContainEquivalentOf("isCorrect");
    }
}

// ── S4 review (student) test mirrors — separate from the production DTOs; Status is the string union. ──────────
public sealed record StudentReviewOptionResponse(Guid Id, int Order, string Text, bool IsCorrect);

public sealed record StudentReviewQuestionResponse(
    Guid Id,
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    string? HintUrl,
    List<StudentReviewOptionResponse> Options,
    Guid? SelectedOptionId,
    bool IsCorrect);

public sealed record StudentAssignmentReviewResponse(
    Guid Id,
    Guid SessionId,
    string? SessionTitle,
    string Status,
    int CorrectCount,
    int QuestionCount,
    int ScoreMarks,
    int MaxMarks,
    int Percent,
    int TimeSpentSeconds,
    DateTimeOffset CompletedAtUtc,
    List<StudentReviewQuestionResponse> Questions);
