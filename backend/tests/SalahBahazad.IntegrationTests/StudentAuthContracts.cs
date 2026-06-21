using System.Text.Json.Serialization;

namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the auth API responses (kept separate from the production DTOs).</summary>
public sealed record StudentAuthMirror(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    StudentInfoMirror Student);

public sealed record StudentInfoMirror(
    Guid Id,
    string FullName,
    string Status,
    BoundDeviceMirror? BoundDevice);

public sealed record BoundDeviceMirror(string? Summary, DateTimeOffset BoundAtUtc);

/// <summary>Just the wire fields the staff client reads — the staff sub-object is irrelevant to these tests.</summary>
public sealed record AuthTokenMirror(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>ProblemDetails with the machine <c>reason</c> extension (frozen contract §1.4).</summary>
public sealed record ProblemReasonMirror(
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("detail")] string? Detail);
