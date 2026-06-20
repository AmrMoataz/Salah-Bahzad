using System.Net;
using FluentAssertions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The proctored quiz engine driven with a student JWT (contract §A, FR-PLAT-QZ-003/007/008): start (randomised,
/// no <c>isCorrect</c>) → answer → submit → best-of + <c>≥</c> pass; attempts-exhausted/active 409; plus IDOR
/// and default-deny.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class QuizEngineTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Happy_path_start_answer_submit_grades_and_unlocks()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, attemptCount: 3, minPassPercent: 60);

        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        attempt.Number.Should().Be(1);
        attempt.Questions.Should().HaveCount(2); // the snapshotted QuestionCount subset
        attempt.Questions.Should().OnlyContain(q => q.Options.Count == 2);
        attempt.DeadlineUtc.Should().BeAfter(attempt.ServerNowUtc);

        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 2); // both correct ⇒ 100%
        var result = await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        result.ScorePercent.Should().Be(100);
        result.Status.Should().Be("Submitted");
        result.BestPercent.Should().Be(100);
        result.Passed.Should().BeTrue();
        result.AttemptsRemaining.Should().Be(2);

        // The summary now reflects the graded attempt + videos-unlocked state.
        var quiz = await ctx.Student.GetMyQuizAsync(ctx.GatedSessionId);
        quiz.Passed.Should().BeTrue();
        quiz.BestPercent.Should().Be(100);
        quiz.AttemptsUsed.Should().Be(1);
        quiz.Attempts.Should().ContainSingle().Which.Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task Started_attempt_never_exposes_isCorrect()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);
        var raw = await (await ctx.Student.StartAttemptRawAsync(ctx.Quiz.Id)).Content.ReadAsStringAsync();
        raw.Should().NotContainEquivalentOf("isCorrect");
    }

    [Fact]
    public async Task Pass_is_inclusive_at_exactly_the_minimum() // FR-PLAT-QZ-008 (#7): >= not >
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, minPassPercent: 50);

        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 1); // exactly 50%
        var result = await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        result.ScorePercent.Should().Be(50);
        result.BestPercent.Should().Be(50);
        result.Passed.Should().BeTrue(); // 50 >= 50 — the boundary the bug fix enables
    }

    [Fact]
    public async Task Best_of_keeps_the_higher_score_across_attempts()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, attemptCount: 3, minPassPercent: 90);

        var first = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(first, correctCount: 2); // 100%
        await ctx.Student.SubmitAttemptAsync(first.AttemptId);

        var second = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(second, correctCount: 1); // 50%
        var result = await ctx.Student.SubmitAttemptAsync(second.AttemptId);

        result.BestPercent.Should().Be(100); // the lower later attempt never lowers the best
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task Attempts_exhausted_returns_409()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1, attemptCount: 1);

        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 1);
        await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        (await ctx.Student.StartAttemptRawAsync(ctx.Quiz.Id))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Starting_a_second_attempt_while_one_is_active_returns_409()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1, attemptCount: 3);
        await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);

        (await ctx.Student.StartAttemptRawAsync(ctx.Quiz.Id))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Answering_or_submitting_a_terminal_attempt_returns_409()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1, attemptCount: 2);

        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        var q = attempt.Questions.Single();
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 1);
        await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        (await ctx.Student.AnswerQuizAsync(attempt.AttemptId, q.Id, q.Options.First().Id))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ctx.Student.SubmitAttemptRawAsync(attempt.AttemptId))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_student_cannot_start_another_students_quiz()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1);

        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var otherStudent = await factory.SeedStudentAsync(ctx.Tenant, ctx.GradeId, cityId, regionId, StudentStatus.Active);
        var otherClient = factory.CreateClientForStudent(ctx.Tenant, otherStudent.Id);

        // GUID-in-URL is not authorization (NFR-SEC-007): starting A's quiz as B → 403.
        (await otherClient.StartAttemptRawAsync(ctx.Quiz.Id))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // B has no quiz for the gated session → 404.
        (await otherClient.GetAsync($"/api/me/quizzes/by-session/{ctx.GatedSessionId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Engine_is_student_only_default_deny()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1);
        var path = $"/api/me/quizzes/by-session/{ctx.GatedSessionId}";

        (await factory.CreateClient().GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClientFor(StaffRole.Teacher, ctx.Tenant).GetAsync(path))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
