namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the Phase-5A audit/dashboard API responses (contract §1/§2), kept
/// separate from the production DTOs so a field rename in either is caught here.</summary>
public sealed record AuditFeedItemResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string ActorType,
    string? ActorRole,
    string? ActorName,
    string Action,
    string Category,
    string? Summary,
    string? TargetType,
    Guid? TargetId,
    string? TargetLabel,
    string? Portal,
    string? IpAddress);

public sealed record PagedAuditResponse(List<AuditFeedItemResponse> Items, int Total, int Page, int PageSize);

public sealed record EnrollmentDayResponse(DateOnly Date, int Count);

public sealed record DashboardResponse(
    int PendingApprovals,
    int ActiveStudents,
    int CodesUsed,
    int CodesActive,
    decimal RevenueFromCodes,
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    List<EnrollmentDayResponse> EnrollmentsByDay,
    int EnrollmentsTotal,
    List<AuditFeedItemResponse> RecentActivity);
