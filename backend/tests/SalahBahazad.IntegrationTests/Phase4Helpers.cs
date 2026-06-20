using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace SalahBahazad.IntegrationTests;

/// <summary>Shared HTTP helpers for the Phase 4 code/enrollment tests.</summary>
internal static class Phase4Helpers
{
    public static async Task<CodeBatchResult> GenerateBatchAsync(
        this HttpClient client, Guid sessionId, int quantity, decimal? value = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/codes/batches", new GenerateCodesRequestBody(sessionId, value, quantity), TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CodeBatchResult>(TestJson.Options))!;
    }

    /// <summary>Reads a batch's codes via the register (needs CodesRead) — the way to get minted serials.</summary>
    public static async Task<List<CodeListItem>> ListBatchCodesAsync(this HttpClient client, Guid batchId)
    {
        var page = await client.GetFromJsonAsync<PagedCodeResponse>(
            $"/api/codes?batchId={batchId}&pageSize=100", TestJson.Options);
        return page!.Items;
    }

    /// <summary>Generates one code for a session and returns its serial (via the teacher register read).</summary>
    public static async Task<string> GenerateOneSerialAsync(
        this HttpClient teacher, Guid sessionId, decimal? value = null)
    {
        var batch = await teacher.GenerateBatchAsync(sessionId, quantity: 1, value: value);
        var codes = await teacher.ListBatchCodesAsync(batch.BatchId);
        return codes.Single().Serial;
    }
}
