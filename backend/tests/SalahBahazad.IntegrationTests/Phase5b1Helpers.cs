using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace SalahBahazad.IntegrationTests;

/// <summary>Shared HTTP helpers for the Phase 5B-1 assignment engine tests (driven with a student JWT, like #12).</summary>
internal static class Phase5b1Helpers
{
    public static async Task<EnrollmentResult> RedeemAsync(this HttpClient studentClient, string serial)
    {
        var response = await studentClient.PostAsJsonAsync(
            "/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!;
    }

    public static async Task<StudentAssignmentResponse> GetMyAssignmentAsync(
        this HttpClient studentClient, Guid sessionId)
        => (await studentClient.GetFromJsonAsync<StudentAssignmentResponse>(
            $"/api/me/assignments/by-session/{sessionId}", TestJson.Options))!;

    public static Task<HttpResponseMessage> AnswerAsync(
        this HttpClient studentClient, Guid assignmentId, Guid questionId, Guid optionId)
        => studentClient.PutAsJsonAsync(
            $"/api/me/assignments/{assignmentId}/questions/{questionId}/answer",
            new AnswerBody(optionId), TestJson.Options);

    /// <summary>Answers every question with the seeded correct option ("A") to drive the assignment to Completed.</summary>
    public static async Task CompleteAssignmentCorrectlyAsync(this HttpClient studentClient, Guid sessionId)
    {
        var assignment = await studentClient.GetMyAssignmentAsync(sessionId);
        foreach (var q in assignment.Questions)
        {
            var correct = q.Options.Single(o => o.Text == "A");
            (await studentClient.AnswerAsync(assignment.Id, q.Id, correct.Id))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
