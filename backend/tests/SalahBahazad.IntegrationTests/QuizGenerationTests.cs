using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Quiz generation on enrol (contract §A, FR-PLAT-QZ-001/002): generated from the <b>prerequisite's</b> bank +
/// settings, idempotent, and only when a prerequisite has a <c>QuizSetting</c> and quiz-eligible questions.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class QuizGenerationTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Enrolling_in_the_gated_session_generates_a_quiz_from_the_prerequisite_settings()
    {
        var ctx = await factory.SetupGatedQuizAsync(
            timeLimitMinutes: 20, questionCount: 2, attemptCount: 3, minPassPercent: 60, eligibleQuestions: 4);

        ctx.Quiz.GatedSessionId.Should().Be(ctx.GatedSessionId);
        ctx.Quiz.Settings.Should().Be(new QuizSettings(20, 2, 3, 60)); // snapshot of A's settings
        ctx.Quiz.AttemptsUsed.Should().Be(0);
        ctx.Quiz.AttemptsRemaining.Should().Be(3);
        ctx.Quiz.BestPercent.Should().BeNull();
        ctx.Quiz.Passed.Should().BeFalse();
        ctx.Quiz.ActiveAttemptId.Should().BeNull();
        ctx.Quiz.Attempts.Should().BeEmpty();

        // Idempotent: exactly one quiz per enrollment (FR-PLAT-ENR-003; the unique index backs it).
        var quizCount = await factory.QueryDbAsync(db => db.UserQuizzes
            .IgnoreQueryFilters()
            .CountAsync(q => q.EnrollmentId == ctx.EnrollmentId));
        quizCount.Should().Be(1);
    }

    [Fact]
    public async Task No_quiz_when_the_prerequisite_has_no_quiz_settings()
    {
        var (tenant, gradeId, specId, student) = await SeedActorsAsync();

        var sourceA = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 2);
        // No SetQuizSettingsAsync on A.
        var gatedB = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        await factory.SetSessionPrerequisiteAsync(gatedB.Id, sourceA.Id);

        await EnrolThroughGateAsync(tenant, sourceA.Id, gatedB.Id, student.Id);

        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        (await studentClient.GetAsync($"/api/me/quizzes/by-session/{gatedB.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_quiz_when_the_prerequisite_has_no_quiz_eligible_questions()
    {
        var (tenant, gradeId, specId, student) = await SeedActorsAsync();

        // A has questions (so its assignment exists for the ENR-007 gate) but none are quiz-eligible.
        var sourceA = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        await factory.SeedQuestionAsync(tenant, sourceA.Id, isValidForQuiz: false);
        await factory.SetQuizSettingsAsync(sourceA.Id, 20, 1, 3, 60);
        var gatedB = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        await factory.SetSessionPrerequisiteAsync(gatedB.Id, sourceA.Id);

        await EnrolThroughGateAsync(tenant, sourceA.Id, gatedB.Id, student.Id);

        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        (await studentClient.GetAsync($"/api/me/quizzes/by-session/{gatedB.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(Guid Tenant, Guid GradeId, Guid SpecId, Domain.Entities.Student Student)> SeedActorsAsync()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        return (tenant, gradeId, specId, student);
    }

    /// <summary>Enrol + complete A's assignment (ENR-007) then enrol B.</summary>
    private async Task EnrolThroughGateAsync(Guid tenant, Guid sourceA, Guid gatedB, Guid studentId)
    {
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, studentId);

        var aSerial = await teacher.GenerateOneSerialAsync(sourceA);
        await studentClient.RedeemAsync(aSerial);
        await studentClient.CompleteAssignmentCorrectlyAsync(sourceA);

        var bSerial = await teacher.GenerateOneSerialAsync(gatedB);
        await studentClient.RedeemAsync(bSerial);
    }
}
