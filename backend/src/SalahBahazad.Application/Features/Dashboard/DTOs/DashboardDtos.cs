using SalahBahazad.Application.Features.Audit.DTOs;

namespace SalahBahazad.Application.Features.Dashboard.DTOs;

/// <summary>
/// The operational dashboard (contract #2, FR-ADM-DASH-001..003, scrDashboard): 4 KPIs (codes is the used/active
/// pair), revenue by redeemed code, a daily enrollments series for the chart, and the 7 most recent NON-sensitive,
/// tenant-scoped activity rows. All counts are tenant-scoped; KPI sources auto-filter, the audit source does not
/// (the handler filters it explicitly — NFR-SEC-010). StatCard trend deltas are demo-only → omitted in 5A.
/// </summary>
public sealed record DashboardDto(
    int PendingApprovals,                          // Students.Status == Pending
    int ActiveStudents,                            // Students.Status == Active
    int CodesUsed,                                 // Codes.Status == Used   ┐ "Codes used / active"
    int CodesActive,                               // Codes.Status == Active ┘
    decimal RevenueFromCodes,                      // Σ Code.Value where Status == Used (EGP)
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    IReadOnlyList<EnrollmentDayDto> EnrollmentsByDay,  // daily granularity; the frontend buckets to weekly
    int EnrollmentsTotal,                          // Σ EnrollmentsByDay
    IReadOnlyList<AuditFeedItem> RecentActivity);  // up to 7, tenant-filtered, non-sensitive, newest first

/// <summary>One day of the enrollments bar chart. <see cref="Date"/> serialises as <c>yyyy-MM-dd</c>.</summary>
public sealed record EnrollmentDayDto(DateOnly Date, int Count);
