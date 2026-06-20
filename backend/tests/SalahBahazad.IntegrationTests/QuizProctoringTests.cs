using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Infrastructure.Jobs;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The proctoring guarantees: the authoritative Hangfire timer auto-submits at the deadline (FR-PLAT-QZ-005),
/// a lost SignalR connection forfeits the active attempt (FR-PLAT-QZ-004), focus-loss is recorded but never
/// forfeits (FR-PLAT-QZ-006), and the audit actor split (start/submit = Student; timeout/forfeit = System;
/// focus-loss = no audit row, FR-PLAT-QZ-010). Uses a real <see cref="HubConnection"/> over the test server.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class QuizProctoringTests(SalahBahazadApiFactory factory)
{
    private Task<SalahBahazad.Domain.Entities.QuizAttempt> ReadAttemptAsync(Guid quizId, Guid attemptId) =>
        factory.QueryDbAsync(async db =>
        {
            var quiz = await db.UserQuizzes.IgnoreQueryFilters().FirstAsync(q => q.Id == quizId);
            return quiz.Attempts.First(a => a.Id == attemptId);
        });

    [Fact]
    public async Task Timer_auto_submits_an_in_progress_attempt_as_TimedOut_by_System()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, minPassPercent: 60);
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 1); // 50% of what was answered

        // Run the exact job Hangfire fires at the deadline.
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<QuizAutoSubmitJob>();
            await job.RunAsync(ctx.Quiz.Id, attempt.AttemptId, ctx.Tenant);
        }

        var timedOut = await ReadAttemptAsync(ctx.Quiz.Id, attempt.AttemptId);
        timedOut.Status.Should().Be(QuizAttemptStatus.TimedOut);
        timedOut.ScorePercent.Should().Be(50);

        var audit = await factory.LatestAuditAsync(ctx.Tenant, "UserQuiz", "QuizAttemptTimedOut");
        audit.Should().NotBeNull();
        audit!.ActorType.Should().Be("System");
        audit.ActorId.Should().BeNull();
    }

    [Fact]
    public async Task Disconnecting_the_hub_forfeits_the_active_attempt_by_System()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2, minPassPercent: 60);
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 2); // would be 100% if it were submitted

        var token = factory.CreateStudentToken(ctx.Tenant, ctx.StudentId);
        var connection = factory.BuildQuizHubConnection(token);
        await connection.StartAsync();
        await connection.DisposeAsync(); // drop the single sitting → forfeit-on-disconnect

        var forfeited = await Phase5b2Helpers.EventuallyAsync(
            () => ReadAttemptAsync(ctx.Quiz.Id, attempt.AttemptId),
            until: a => a.Status == QuizAttemptStatus.Forfeited);

        forfeited.Status.Should().Be(QuizAttemptStatus.Forfeited);
        forfeited.ScorePercent.Should().Be(0); // consumed, scored 0 despite the correct answers

        var audit = await factory.LatestAuditAsync(ctx.Tenant, "UserQuiz", "QuizAttemptForfeited");
        audit.Should().NotBeNull();
        audit!.ActorType.Should().Be("System");
    }

    [Fact]
    public async Task Focus_events_are_recorded_and_never_forfeit_the_attempt()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 2);
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        var now = DateTimeOffset.UtcNow;

        (await ctx.Student.RecordFocusAsync(attempt.AttemptId, "FocusLost", now, 4000))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.Student.RecordFocusAsync(attempt.AttemptId, "FocusReturned", now.AddSeconds(4)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Recorded in assessment_events (high-volume), keyed by the attempt.
        var eventCount = await factory.QueryDbAsync(db => db.AssessmentEvents
            .IgnoreQueryFilters().CountAsync(e => e.QuizAttemptId == attempt.AttemptId));
        eventCount.Should().Be(2);

        // The attempt is untouched — monitoring only (FR-PLAT-QZ-006).
        (await ReadAttemptAsync(ctx.Quiz.Id, attempt.AttemptId)).Status.Should().Be(QuizAttemptStatus.InProgress);

        // Focus-loss writes NO audit row.
        var focusAudit = await factory.QueryDbAsync(db => db.AuditEntries
            .IgnoreQueryFilters().CountAsync(a => a.TenantId == ctx.Tenant && a.Action.Contains("Focus")));
        focusAudit.Should().Be(0);
    }

    [Fact]
    public async Task A_non_focus_event_type_on_the_focus_path_is_400()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1);
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);

        (await ctx.Student.RecordFocusAsync(attempt.AttemptId, "Answered", DateTimeOffset.UtcNow))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_and_submit_are_audited_as_the_student()
    {
        var ctx = await factory.SetupGatedQuizAsync(questionCount: 1);
        var attempt = await ctx.Student.StartAttemptAsync(ctx.Quiz.Id);
        await ctx.Student.AnswerAttemptAsync(attempt, correctCount: 1);
        await ctx.Student.SubmitAttemptAsync(attempt.AttemptId);

        var started = await factory.LatestAuditAsync(ctx.Tenant, "UserQuiz", "QuizAttemptStarted");
        started!.ActorType.Should().Be("Student");
        started.ActorId.Should().Be(ctx.StudentId);

        var submitted = await factory.LatestAuditAsync(ctx.Tenant, "UserQuiz", "QuizAttemptSubmitted");
        submitted!.ActorType.Should().Be("Student");
        submitted.ActorId.Should().Be(ctx.StudentId);
    }

    [Fact]
    public async Task Hub_rejects_a_connection_with_no_token()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"{factory.Server.BaseAddress}hubs/quiz", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var act = async () => await connection.StartAsync();
        await act.Should().ThrowAsync<Exception>(); // 401 at the handshake

        await connection.DisposeAsync();
    }
}
