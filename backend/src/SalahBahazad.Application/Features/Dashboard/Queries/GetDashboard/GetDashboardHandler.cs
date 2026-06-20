using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Audit;
using SalahBahazad.Application.Features.Dashboard.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Dashboard.Queries.GetDashboard;

internal sealed class GetDashboardHandler(IAppDbContext db, ICurrentTenantResolver tenant, TimeProvider clock)
    : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async ValueTask<DashboardDto> Handle(GetDashboardQuery query, CancellationToken cancellationToken)
    {
        // Period: explicit From/To wins; else the 7d/30d/90d token; else default 30d (contract #2).
        var to = query.To ?? clock.GetUtcNow();
        var from = query.From ?? to.AddDays(-(AuditPeriod.Days(query.Period) ?? 30));

        // ── KPIs — point-in-time totals over the auto-tenant-filtered DbSets (no manual tenant Where) ──
        var pendingApprovals = await db.Students.CountAsync(s => s.Status == StudentStatus.Pending, cancellationToken);
        var activeStudents = await db.Students.CountAsync(s => s.Status == StudentStatus.Active, cancellationToken);
        var codesUsed = await db.Codes.CountAsync(c => c.Status == CodeStatus.Used, cancellationToken);
        var codesActive = await db.Codes.CountAsync(c => c.Status == CodeStatus.Active, cancellationToken);
        var revenueFromCodes = await db.Codes
            .Where(c => c.Status == CodeStatus.Used)
            .SumAsync(c => c.Value, cancellationToken);

        // ── Enrollments by day over [from, to] — group server-side-fetched timestamps, then zero-fill ──
        var enrolledTimestamps = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.EnrolledAtUtc >= from && e.EnrolledAtUtc <= to)
            .Select(e => e.EnrolledAtUtc)
            .ToListAsync(cancellationToken);

        var countByDay = enrolledTimestamps
            .GroupBy(ts => DateOnly.FromDateTime(ts.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());

        var fromDate = DateOnly.FromDateTime(from.UtcDateTime);
        var toDate = DateOnly.FromDateTime(to.UtcDateTime);
        var enrollmentsByDay = new List<EnrollmentDayDto>();
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
            enrollmentsByDay.Add(new EnrollmentDayDto(day, countByDay.GetValueOrDefault(day, 0)));
        var enrollmentsTotal = enrollmentsByDay.Sum(d => d.Count);

        // ── Recent activity — the 7 newest, tenant-filtered EXPLICITLY (NFR-SEC-010), non-sensitive ──
        var recentRows = await db.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == tenant.TenantId)
            .Where(a => !EF.Constant(SensitiveAuditActions.All).Contains(a.Action))
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(7)
            .ToListAsync(cancellationToken);
        var recentActivity = await AuditFeedProjector.ToFeedItemsAsync(db, recentRows, cancellationToken);

        return new DashboardDto(
            pendingApprovals,
            activeStudents,
            codesUsed,
            codesActive,
            revenueFromCodes,
            from,
            to,
            enrollmentsByDay,
            enrollmentsTotal,
            recentActivity);
    }
}
