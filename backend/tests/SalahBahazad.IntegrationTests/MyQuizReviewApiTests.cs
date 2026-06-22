using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Infrastructure.Jobs;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The student per-attempt answer-key review — <c>GET /api/me/quizzes/attempts/{attemptId}/review</c> (contract §B,
/// FR-STU-QZ-009): the <b>only</b> student surface that exposes quiz option correctness, gated to the caller's own
/// <b>terminal</b> attempt. Proves the 200 answer-key + score, the additive attempt <c>id</c> round-trip
/// (intro → review), the 403 <c>quiz_attempt_in_progress</c> gate, the 404 IDOR/cross-tenant boundary
/// (NFR-SEC-007/010), student-only default-deny, the read is not audited (§E), and that the student/staff
/// <c>isCorrect</c> split holds (live + intro shapes stay correctness-free; only the review reveals the key).
/// The 5B-2 quiz engine is reused as-is.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyQuizReviewApiTests(SalahBahazadApiFactory factory)
{
    /// <summary>Starts an attempt, answers Q1 correct ("A") + Q2 wrong ("B") so the 2-question attempt grades to a
    /// Submitted 50% (1/2 marks) — the mixed score the review must report. Returns the started attempt, the two
    /// picked option ids (for echo assertions), and the submit result.</summary>
    private static async Task<(QuizAttemptResponse Attempt, Guid Q1PickA, Guid Q2PickB, QuizAttemptResult Result)>
        CompleteMixedAttemptAsync(GatedQuizContext ctx)
    {
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        var ordered = attempt.Questions.OrderBy(q => q.Order).ToList();
        var q1A = ordered[0].Options.Single(o => o.Text == "A").Id; // answered correctly
        var q2B = ordered[1].Options.Single(o => o.Text == "B").Id; // answered wrong
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 1); // 1 of 2 ⇒ 50%
        var result = await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);
        return (attempt, q1A, q2B, result);
    }

    private async Task<HttpClient> SeedStudentInAnotherTenantAsync()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, _) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        return factory.CreateClientForStudent(tenant, student.Id);
    }

    [Fact]
    public async Task Review_returns_the_answer_key_and_score_for_the_callers_terminal_attempt()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, attemptCount: 3, minPassPercent: 60);
        var start = DateTimeOffset.UtcNow;
        var (attempt, q1A, q2B, result) = await CompleteMixedAttemptAsync(ctx);
        result.ScorePercent.Should().Be(50); // sanity: 1 of 2 correct

        var review = await ctx.Student.GetFromJsonAsync<StudentQuizAttemptReviewResponse>(
            $"/api/me/quizzes/attempts/{attempt.AttemptId}/review", TestJson.Options);

        review!.AttemptId.Should().Be(attempt.AttemptId);   // echoes the route param
        review.QuizId.Should().Be(ctx.Quiz.Id);
        review.GatedSessionId.Should().Be(ctx.GatedSessionId);
        review.Number.Should().Be(1);
        review.Status.Should().Be("Submitted");
        review.ScorePercent.Should().Be(result.ScorePercent); // straight off the snapshot
        review.ScorePercent.Should().Be(50);
        review.MinPassPercent.Should().Be(60);
        review.StartedAtUtc.Should().BeOnOrAfter(start.AddSeconds(-5));
        review.SubmittedAtUtc.Should().BeOnOrAfter(review.StartedAtUtc);
        review.TimeSpentSeconds.Should().BeGreaterThanOrEqualTo(0);

        // sessionTitle resolves to gated session B's title (via IgnoreQueryFilters).
        var expectedTitle = await factory.QueryDbAsync(db => db.Sessions
            .IgnoreQueryFilters().Where(s => s.Id == ctx.GatedSessionId).Select(s => s.Title).FirstAsync());
        review.SessionTitle.Should().Be(expectedTitle);

        // Questions ordered by Order; every question's options ordered, with the answer key (isCorrect) exposed.
        review.Questions.Select(q => q.Order).Should().Equal(1, 2);
        foreach (var q in review.Questions)
        {
            q.Options.Select(o => o.Order).Should().BeInAscendingOrder();
            q.Options.Should().ContainSingle(o => o.IsCorrect).Which.Text.Should().Be("A");
            q.Mark.Should().Be(1);
        }

        var rq1 = review.Questions.Single(q => q.Order == 1);
        rq1.IsCorrect.Should().BeTrue();                    // per-question correctness
        rq1.SelectedOptionId.Should().Be(q1A);              // the student's pick is echoed

        var rq2 = review.Questions.Single(q => q.Order == 2);
        rq2.IsCorrect.Should().BeFalse();
        rq2.SelectedOptionId.Should().Be(q2B);
    }

    [Fact]
    public async Task Additive_attempt_id_appears_on_the_intro_and_addresses_the_review()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);
        var (attempt, _, _, _) = await CompleteMixedAttemptAsync(ctx);

        // The intro's attempts[] now carries the attempt id (the one additive field, §A #1)...
        var quiz = await ctx.Student.GetMyQuizAsync(ctx.GatedSessionId);
        var summary = quiz.Attempts.Should().ContainSingle().Subject;
        summary.Id.Should().Be(attempt.AttemptId);

        // ...and that id round-trips to the §B review for the same terminal attempt.
        var review = await ctx.Student.GetFromJsonAsync<StudentQuizAttemptReviewResponse>(
            $"/api/me/quizzes/attempts/{summary.Id}/review", TestJson.Options);
        review!.AttemptId.Should().Be(summary.Id);
    }

    [Fact]
    public async Task In_progress_attempt_is_403_with_quiz_attempt_in_progress_reason()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);

        // Start but do not submit → the single active attempt is InProgress → the key stays hidden mid-sitting.
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);

        var response = await ctx.Student.GetAsync($"/api/me/quizzes/attempts/{attempt.AttemptId}/review");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemReasonMirror>(TestJson.Options);
        problem!.Reason.Should().Be("quiz_attempt_in_progress");
        problem.Detail.Should().Be("Finish the quiz to see your answers and score.");
    }

    [Fact]
    public async Task Review_is_404_for_an_unknown_or_another_students_attempt()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);
        var (attempt, _, _, _) = await CompleteMixedAttemptAsync(ctx);

        // An unknown id → opaque 404.
        (await ctx.Student.GetAsync($"/api/me/quizzes/attempts/{Guid.NewGuid()}/review"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Another student (same tenant) cannot read A's terminal attempt — a GUID in the URL is not authorization
        // (NFR-SEC-007) → 404, never A's data.
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var otherStudent = await factory.SeedStudentAsync(ctx.Tenant, ctx.GradeId, cityId, regionId, StudentStatus.Active);
        var otherClient = factory.CreateClientForStudent(ctx.Tenant, otherStudent.Id);
        (await otherClient.GetAsync($"/api/me/quizzes/attempts/{attempt.AttemptId}/review"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Review_is_404_across_tenants()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);
        var (attempt, _, _, _) = await CompleteMixedAttemptAsync(ctx);

        // A student in another tenant cannot see tenant A's attempt (the global query filter) → 404 (NFR-SEC-010).
        var clientB = await SeedStudentInAnotherTenantAsync();
        (await clientB.GetAsync($"/api/me/quizzes/attempts/{attempt.AttemptId}/review"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Review_is_student_only_default_deny()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);
        var (attempt, _, _, _) = await CompleteMixedAttemptAsync(ctx);
        var path = $"/api/me/quizzes/attempts/{attempt.AttemptId}/review";

        (await factory.CreateClient().GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized); // anon
        (await ctx.Teacher.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);                // staff
    }

    [Fact]
    public async Task Reading_the_review_is_not_audited()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);
        var (attempt, _, _, _) = await CompleteMixedAttemptAsync(ctx);

        var before = await factory.QueryDbAsync(db => db.AuditEntries.CountAsync(a => a.TenantId == ctx.Tenant));
        (await ctx.Student.GetAsync($"/api/me/quizzes/attempts/{attempt.AttemptId}/review"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await factory.QueryDbAsync(db => db.AuditEntries.CountAsync(a => a.TenantId == ctx.Tenant));

        after.Should().Be(before); // a pure read of the caller's own attempt writes no audit row (§E)
    }

    [Fact]
    public async Task Only_the_review_exposes_isCorrect_not_the_live_or_intro_shapes()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);

        // The intro shape never carries correctness (the 5B-2 invariant)...
        (await ctx.Student.GetStringAsync($"/api/me/quizzes/by-session/{ctx.GatedSessionId}"))
            .Should().NotContainEquivalentOf("isCorrect");

        // ...nor does the live (started) attempt shape...
        var startRaw = await (await ctx.Student.StartAttemptRawAsync(ctx.Quiz.Id)).Content.ReadAsStringAsync();
        startRaw.Should().NotContainEquivalentOf("isCorrect");
        var attempt = JsonSerializer.Deserialize<QuizAttemptResponse>(startRaw, TestJson.Options)!;

        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 1);
        await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        // ...but the post-terminal review IS the one surface that does (per-option + per-question isCorrect).
        (await ctx.Student.GetStringAsync($"/api/me/quizzes/attempts/{attempt.AttemptId}/review"))
            .Should().ContainEquivalentOf("isCorrect");
    }

    [Fact]
    public async Task Review_of_a_timed_out_attempt_echoes_the_status_and_the_unanswered_question_key()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, minPassPercent: 60);
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);

        // Answer only Q1 correctly ("A") and leave Q2 unanswered, then let the deadline auto-submit (TimedOut).
        var ordered = attempt.Questions.OrderBy(q => q.Order).ToList();
        var q1A = ordered[0].Options.Single(o => o.Text == "A").Id;
        (await ctx.Student.AnswerQuizAsync(attempt.AttemptId, ordered[0].Id, q1A))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Run the exact job Hangfire fires at the deadline (the authoritative auto-submit, FR-PLAT-QZ-005).
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<QuizAutoSubmitJob>();
            await job.RunAsync(ctx.Quiz.Id, attempt.AttemptId, ctx.Tenant);
        }

        var review = await ctx.Student.GetFromJsonAsync<StudentQuizAttemptReviewResponse>(
            $"/api/me/quizzes/attempts/{attempt.AttemptId}/review", TestJson.Options);

        review!.Status.Should().Be("TimedOut");        // the terminal status is echoed verbatim (§B.1)
        review.ScorePercent.Should().Be(50);           // graded what was answered (1 of 2 marks)

        var rq1 = review.Questions.Single(q => q.Order == 1);
        rq1.SelectedOptionId.Should().Be(q1A);         // the answered pick echoes
        rq1.IsCorrect.Should().BeTrue();

        var rq2 = review.Questions.Single(q => q.Order == 2);
        rq2.SelectedOptionId.Should().BeNull();        // unanswered → null (common on a TimedOut attempt, §B.1)
        rq2.IsCorrect.Should().BeFalse();
        rq2.Options.Should().ContainSingle(o => o.IsCorrect); // the key still reveals the correct option
    }

    [Fact]
    public async Task Review_of_a_forfeited_attempt_is_score_zero_with_no_selected_options()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, minPassPercent: 60);
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id); // started, nothing answered

        // Forfeit the active attempt (score 0, consumed, FR-PLAT-QZ-004). The hub's forfeit-on-disconnect mechanism
        // is proven separately (QuizProctoringTests); here we only need a Forfeited attempt to review, so we drive
        // the domain directly — deterministic, no SignalR timing race.
        await factory.QueryDbAsync(async db =>
        {
            var quiz = await db.UserQuizzes.IgnoreQueryFilters().FirstAsync(q => q.Id == ctx.Quiz.Id);
            quiz.ForfeitActiveAttempt(DateTimeOffset.UtcNow);
            return await db.SaveChangesAsync();
        });

        var review = await ctx.Student.GetFromJsonAsync<StudentQuizAttemptReviewResponse>(
            $"/api/me/quizzes/attempts/{attempt.AttemptId}/review", TestJson.Options);

        review!.Status.Should().Be("Forfeited");
        review.ScorePercent.Should().Be(0);            // a forfeited attempt scores 0 (§B.1)
        review.Questions.Should().OnlyContain(q => q.SelectedOptionId == null && !q.IsCorrect); // fully unanswered
        review.Questions.Should().OnlyContain(q => q.Options.Any(o => o.IsCorrect)); // the key is still present
    }
}

// ── S5 review (student) test mirrors — separate from the production DTOs; Status is the string union. ──────────
public sealed record StudentQuizReviewOptionResponse(Guid Id, int Order, string Text, bool IsCorrect);

public sealed record StudentQuizReviewQuestionResponse(
    Guid Id,
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    List<StudentQuizReviewOptionResponse> Options,
    Guid? SelectedOptionId,
    bool IsCorrect);

public sealed record StudentQuizAttemptReviewResponse(
    Guid AttemptId,
    Guid QuizId,
    Guid GatedSessionId,
    string? SessionTitle,
    int Number,
    string Status,
    int ScorePercent,
    int MinPassPercent,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset SubmittedAtUtc,
    int TimeSpentSeconds,
    List<StudentQuizReviewQuestionResponse> Questions);
