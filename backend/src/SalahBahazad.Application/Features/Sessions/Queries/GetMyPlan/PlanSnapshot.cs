using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMyPlan;

/// <summary>
/// The cached projection of a student's weekly plan (contract §C). Identical to <see cref="MyPlanDto"/> except the
/// focus carries the thumbnail's R2 <b>object key</b> rather than a signed URL: the snapshot is cached for the
/// whole ISO week, but a signed URL must stay short-lived, so it is signed fresh per read in the handler and never
/// cached. Everything else (steps, recent, KPIs) holds no signed URL and is reused verbatim in the response.
/// </summary>
internal sealed record PlanSnapshot(
    string IsoWeek,
    DateTimeOffset WeekStartUtc,
    DateTimeOffset WeekEndUtc,
    DateTimeOffset GeneratedAtUtc,
    int TotalSteps,
    int CompletedSteps,
    int OverdueSteps,
    MyPlanKpisDto Kpis,
    PlanFocusSnapshot? Focus,
    IReadOnlyList<MyPlanStepDto> Steps,
    IReadOnlyList<MyPlanRecentDto> RecentlyEnrolled)
{
    public MyPlanDto ToDto(string? focusThumbnailUrl) => new(
        IsoWeek,
        WeekStartUtc,
        WeekEndUtc,
        GeneratedAtUtc,
        TotalSteps,
        CompletedSteps,
        OverdueSteps,
        Kpis,
        Focus?.ToDto(focusThumbnailUrl),
        Steps,
        RecentlyEnrolled);
}

/// <summary>The cached focus session (contract §A.1) — carries the thumbnail object key; the signed URL is
/// supplied per read.</summary>
internal sealed record PlanFocusSnapshot(
    Guid SessionId,
    string Title,
    string? SpecializationName,
    string? ThumbnailObjectKey,
    int ProgressPercent,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    int? ExpiresInDays,
    MyPlanDueState DueState)
{
    public MyPlanFocusDto ToDto(string? thumbnailUrl) => new(
        SessionId,
        Title,
        SpecializationName,
        thumbnailUrl,
        ProgressPercent,
        ExpiresAtUtc,
        IsExpired,
        ExpiresInDays,
        DueState);
}
