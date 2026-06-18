using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the API responses, kept separate from the production DTOs.</summary>
public sealed record StaffResponse(Guid Id, string DisplayName, string Email, string Role, bool IsActive);

public sealed record PagedStaffResponse(List<StaffResponse> Items, int Total, int Page, int PageSize);

public sealed record ValidationProblemResponse(
    [property: JsonPropertyName("errors")] Dictionary<string, string[]> Errors);

internal static class TestJson
{
    /// <summary>Web defaults + string enums, matching the API's <c>JsonStringEnumConverter</c> (A3).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
