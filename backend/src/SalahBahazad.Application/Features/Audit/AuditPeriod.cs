namespace SalahBahazad.Application.Features.Audit;

/// <summary>
/// Parses the design's period control (<c>7d</c>|<c>30d</c>|<c>90d</c>) shared by the activity feed and the
/// dashboard (contract #1/#2). The endpoints also accept an explicit <c>from</c>/<c>to</c> range (contract §5);
/// each handler decides its own default (the feed: no implicit window; the dashboard: 30d).
/// </summary>
internal static class AuditPeriod
{
    /// <summary>Number of days for a period token, or null when unset/unrecognised.</summary>
    public static int? Days(string? period) => period?.Trim().ToLowerInvariant() switch
    {
        "7d" => 7,
        "30d" => 30,
        "90d" => 90,
        _ => null,
    };
}
