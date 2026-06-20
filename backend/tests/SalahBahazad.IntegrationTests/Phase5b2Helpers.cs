using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The fully-wired gated-quiz context for a 5B-2 test: a prerequisite session A (quiz-eligible bank + settings)
/// gates session B, the student has completed A's assignment (ENR-007) and enrolled in B, so a quiz exists.
/// </summary>
internal sealed record GatedQuizContext(
    Guid Tenant,
    Guid GradeId,
    Guid SpecId,
    Guid StudentId,
    HttpClient Student,
    HttpClient Teacher,
    Guid SourceSessionId,
    Guid GatedSessionId,
    Guid EnrollmentId,
    StudentQuiz Quiz);

/// <summary>Shared HTTP / SignalR helpers for the Phase 5B-2 quiz engine tests.</summary>
internal static class Phase5b2Helpers
{
    /// <summary>
    /// Stands up the real generate-on-enroll flow: seeds A (eligible bank + quiz settings) and B (gated by A),
    /// then enrolls + completes A's assignment and enrolls B — so the side-effect generates the quiz from A.
    /// </summary>
    public static async Task<GatedQuizContext> SetupGatedQuizAsync(
        this SalahBahazadApiFactory factory,
        int timeLimitMinutes = 30,
        int questionCount = 2,
        int attemptCount = 3,
        int minPassPercent = 60,
        int eligibleQuestions = 3)
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);

        // A (prerequisite): quiz-eligible questions + the quiz settings the engine snapshots.
        var sourceA = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: eligibleQuestions);
        await factory.SetQuizSettingsAsync(sourceA.Id, timeLimitMinutes, questionCount, attemptCount, minPassPercent);

        // B (gated): videos but no own bank; its videos are unlocked by passing A's quiz.
        var gatedB = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        await factory.SetSessionPrerequisiteAsync(gatedB.Id, sourceA.Id);

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        // Enrol + complete A (ENR-007), then enrol B → the quiz is generated from A's bank + settings.
        var aSerial = await teacher.GenerateOneSerialAsync(sourceA.Id);
        await studentClient.RedeemAsync(aSerial);
        await studentClient.CompleteAssignmentCorrectlyAsync(sourceA.Id);

        var bSerial = await teacher.GenerateOneSerialAsync(gatedB.Id);
        var bEnrollment = await studentClient.RedeemAsync(bSerial);

        var quiz = await studentClient.GetMyQuizAsync(gatedB.Id);
        return new GatedQuizContext(
            tenant, gradeId, specId, student.Id, studentClient, teacher,
            sourceA.Id, gatedB.Id, bEnrollment.Id, quiz);
    }

    // ── Engine HTTP wrappers ─────────────────────────────────────────────────
    public static async Task<StudentQuiz> GetMyQuizAsync(this HttpClient student, Guid sessionId)
        => (await student.GetFromJsonAsync<StudentQuiz>(
            $"/api/me/quizzes/by-session/{sessionId}", TestJson.Options))!;

    public static Task<HttpResponseMessage> StartAttemptRawAsync(this HttpClient student, Guid quizId)
        => student.PostAsync($"/api/me/quizzes/{quizId}/attempts", content: null);

    public static async Task<QuizAttemptResponse> StartAttemptAsync(this HttpClient student, Guid quizId)
    {
        var response = await student.StartAttemptRawAsync(quizId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QuizAttemptResponse>(TestJson.Options))!;
    }

    public static Task<HttpResponseMessage> AnswerQuizAsync(
        this HttpClient student, Guid attemptId, Guid questionId, Guid optionId)
        => student.PutAsJsonAsync(
            $"/api/me/quizzes/attempts/{attemptId}/questions/{questionId}/answer",
            new AnswerBody(optionId), TestJson.Options);

    public static Task<HttpResponseMessage> SubmitAttemptRawAsync(this HttpClient student, Guid attemptId)
        => student.PostAsync($"/api/me/quizzes/attempts/{attemptId}/submit", content: null);

    public static async Task<QuizAttemptResult> SubmitAttemptAsync(this HttpClient student, Guid attemptId)
    {
        var response = await student.SubmitAttemptRawAsync(attemptId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QuizAttemptResult>(TestJson.Options))!;
    }

    public static Task<HttpResponseMessage> RecordFocusAsync(
        this HttpClient student, Guid attemptId, string type, DateTimeOffset occurredAtUtc, int? durationMs = null)
        => student.PostAsJsonAsync(
            $"/api/me/quizzes/attempts/{attemptId}/focus",
            new QuizFocusBody(type, occurredAtUtc, durationMs), TestJson.Options);

    /// <summary>Answers every question of a started attempt; the first <paramref name="correctCount"/> correctly
    /// (seeded option "A" is correct, "B" wrong).</summary>
    public static async Task AnswerAttemptAsync(
        this HttpClient student, QuizAttemptResponse attempt, int correctCount)
    {
        var ordered = attempt.Questions.OrderBy(q => q.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var q = ordered[i];
            var option = i < correctCount
                ? q.Options.Single(o => o.Text == "A")
                : q.Options.Single(o => o.Text == "B");
            var response = await student.AnswerQuizAsync(attempt.AttemptId, q.Id, option.Id);
            response.EnsureSuccessStatusCode();
        }
    }

    // ── Review HTTP wrapper ──────────────────────────────────────────────────
    public static async Task<QuizReview> GetQuizReviewAsync(this HttpClient staff, Guid enrollmentId)
        => (await staff.GetFromJsonAsync<QuizReview>(
            $"/api/review/quizzes/{enrollmentId}", TestJson.Options))!;

    // ── SignalR hub client (over the in-memory test server, LongPolling transport) ────────────
    public static HubConnection BuildQuizHubConnection(this SalahBahazadApiFactory factory, string accessToken)
        => new HubConnectionBuilder()
            .WithUrl($"{factory.Server.BaseAddress}hubs/quiz?access_token={accessToken}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling; // TestServer has no WebSocket transport
            })
            .Build();

    // ── Polling helper for async server-side effects (forfeit, timeout) ──────────
    public static async Task<T> EventuallyAsync<T>(
        Func<Task<T>> probe, Func<T, bool> until, int timeoutMs = 15000, int pollMs = 200)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var value = await probe();
            if (until(value) || stopwatch.ElapsedMilliseconds > timeoutMs)
                return value;
            await Task.Delay(pollMs);
        }
    }
}
