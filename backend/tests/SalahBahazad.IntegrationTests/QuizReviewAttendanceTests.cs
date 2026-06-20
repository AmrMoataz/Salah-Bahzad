using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Infrastructure.Jobs;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Admin quiz review + the now-real attendance quiz columns (contract §B, FR-ADM-REV-002, FR-PLAT-ATT-002):
/// the per-attempt review with best/flags, the populated <c>bestQuizPercent</c>/<c>quizAttemptCount</c>, plus
/// tenant isolation and default-deny on the staff review.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class QuizReviewAttendanceTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Quiz_review_lists_attempts_with_best_and_flags()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, attemptCount: 3, minPassPercent: 60);

        var a1 = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(a1, correctCount: 1); // 50%
        await ctx.Student.SubmitAttemptAsync(a1.AttemptId);

        var a2 = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(a2, correctCount: 2); // 100%
        await ctx.Student.SubmitAttemptAsync(a2.AttemptId);

        var a3 = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id); // left to the timer → TimedOut
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<QuizAutoSubmitJob>();
            await job.RunAsync(ctx.Quiz.Id, a3.AttemptId, ctx.Tenant);
        }

        var review = await ctx.Teacher.GetQuizReviewAsync(ctx.EnrollmentId);

        review.BestPercent.Should().Be(100);
        review.Passed.Should().BeTrue();
        review.MinPassPercent.Should().Be(60);
        review.AttemptsUsed.Should().Be(3);
        review.AttemptsAllowed.Should().Be(3);
        review.Attempts.Should().HaveCount(3);

        review.Attempts.Single(a => a.Number == 1).Flag.Should().Be("Clean");
        review.Attempts.Single(a => a.Number == 1).IsBest.Should().BeFalse();

        var best = review.Attempts.Single(a => a.Number == 2);
        best.IsBest.Should().BeTrue();
        best.ScorePercent.Should().Be(100);
        best.Status.Should().Be("Submitted");

        var timedOut = review.Attempts.Single(a => a.Number == 3);
        timedOut.Status.Should().Be("TimedOut");
        timedOut.Flag.Should().Be("Timeout");
        timedOut.IsBest.Should().BeFalse();
    }

    [Fact]
    public async Task Attendance_columns_become_real_after_a_quiz_is_graded()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, attemptCount: 3, minPassPercent: 60);

        var a1 = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(a1, correctCount: 2); // 100%
        await ctx.Student.SubmitAttemptAsync(a1.AttemptId);

        var a2 = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(a2, correctCount: 1); // 50%
        await ctx.Student.SubmitAttemptAsync(a2.AttemptId);

        // The gated session's attendance matrix now carries the best-of and attempt count.
        var page = await ctx.Teacher.GetFromJsonAsync<PagedSessionAttendance>(
            $"/api/attendance/sessions/{ctx.GatedSessionId}", TestJson.Options);
        var row = page!.Items.Single(r => r.StudentId == ctx.StudentId);
        row.BestQuizPercent.Should().Be(100);
        row.QuizAttemptCount.Should().Be(2);

        // The student's per-session breakdown agrees.
        var studentPage = await ctx.Teacher.GetFromJsonAsync<PagedStudentAttendance>(
            $"/api/attendance/students/{ctx.StudentId}", TestJson.Options);
        var sessionRow = studentPage!.Items.Single(r => r.SessionId == ctx.GatedSessionId);
        sessionRow.BestQuizPercent.Should().Be(100);
        sessionRow.QuizAttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task Quiz_review_is_tenant_isolated()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1);

        var otherTenant = await factory.SeedTenantAsync();
        var otherTeacher = factory.CreateClientFor(StaffRole.Teacher, otherTenant);

        (await otherTeacher.GetAsync($"/api/review/quizzes/{ctx.EnrollmentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound); // another tenant cannot see it (NFR-SEC-010)
    }

    [Fact]
    public async Task Quiz_review_is_staff_only_default_deny()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1);
        var path = $"/api/review/quizzes/{ctx.EnrollmentId}";

        (await factory.CreateClient().GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ctx.Student.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden); // a student token
    }
}
