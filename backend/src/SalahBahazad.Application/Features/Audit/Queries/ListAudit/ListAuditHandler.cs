using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Audit.DTOs;

namespace SalahBahazad.Application.Features.Audit.Queries.ListAudit;

internal sealed class ListAuditHandler(IAppDbContext db, ICurrentTenantResolver tenant, TimeProvider clock)
    : IRequestHandler<ListAuditQuery, PagedResult<AuditFeedItem>>
{
    public async ValueTask<PagedResult<AuditFeedItem>> Handle(
        ListAuditQuery query, CancellationToken cancellationToken)
    {
        // Resolve the design's period control to a concrete window when no explicit from/to was given. The feed
        // applies a window ONLY when asked: the entity Activity tabs pass no period and must see all history.
        var (from, to) = (query.From, query.To);
        if (from is null && to is null && AuditPeriod.Days(query.Period) is int days)
        {
            to = clock.GetUtcNow();
            from = to.Value.AddDays(-days);
        }

        // AuditEntry is NOT tenant-filtered by the global query filter — scope explicitly (NFR-SEC-010).
        var entries = AuditFilters.Apply(
            db.AuditEntries.AsNoTracking().Where(a => a.TenantId == tenant.TenantId),
            query.ActorId, query.ActorType, query.Category, from, to,
            query.StudentId, query.SessionId, query.EntityType, query.EntityId, query.IncludeSensitive);

        var total = await entries.CountAsync(cancellationToken);

        var rows = await entries
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.Id) // stable tiebreaker — entries in one save share OccurredAtUtc
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = await AuditFeedProjector.ToFeedItemsAsync(db, rows, cancellationToken);
        return new PagedResult<AuditFeedItem>(items, total, query.Page, query.PageSize);
    }
}
