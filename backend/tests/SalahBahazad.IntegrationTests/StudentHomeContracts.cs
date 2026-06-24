namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Loosely-typed mirrors of the student-portal Home API (GET /api/me/plan, contract §A/§B/§E), kept separate from
/// the production DTOs. Enums are string unions matching the API's <c>JsonStringEnumConverter</c>:
/// <c>Kind</c> ("Quiz"|"Videos"|"Assignment"|"Redeem"), <c>Status</c> ("Pending"|"Completed"),
/// <c>DueState</c> ("None"|"ExpiringSoon"|"Expired"), action <c>Type</c> ("Navigate"|"Redeem").
/// </summary>
public sealed record MyPlanProgressResponse(int Done, int Total);

public sealed record MyPlanActionResponse(string Type, string? Route, string Label);

public sealed record MyPlanStepResponse(
    string Key,
    string Kind,
    string Title,
    string? Subtitle,
    Guid SessionId,
    string SessionTitle,
    string? SpecializationName,
    string Status,
    bool Blocked,
    string? BlockedReason,
    string DueState,
    DateTimeOffset? ExpiresAtUtc,
    MyPlanProgressResponse? Progress,
    MyPlanActionResponse Action);

public sealed record MyPlanRecentResponse(
    Guid SessionId, string Title, string? SpecializationName, DateTimeOffset EnrolledAtUtc);

public sealed record MyPlanKpisResponse(
    int ActiveSessions,
    int VideosWatched,
    int VideosTotal,
    int OverallProgressPercent,
    int CompletedSessions);

public sealed record MyPlanFocusResponse(
    Guid SessionId,
    string Title,
    string? SpecializationName,
    string? ThumbnailUrl,
    int ProgressPercent,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    int? ExpiresInDays,
    string DueState);

public sealed record MyPlanResponse(
    string IsoWeek,
    DateTimeOffset WeekStartUtc,
    DateTimeOffset WeekEndUtc,
    DateTimeOffset GeneratedAtUtc,
    int TotalSteps,
    int CompletedSteps,
    int OverdueSteps,
    MyPlanKpisResponse Kpis,
    MyPlanFocusResponse? Focus,
    List<MyPlanStepResponse> Steps,
    List<MyPlanRecentResponse> RecentlyEnrolled);
