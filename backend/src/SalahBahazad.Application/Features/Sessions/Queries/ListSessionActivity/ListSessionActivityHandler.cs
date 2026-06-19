using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListSessionActivity;

internal sealed class ListSessionActivityHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<ListSessionActivityQuery, PagedResult<SessionActivityDto>>
{
    public async ValueTask<PagedResult<SessionActivityDto>> Handle(
        ListSessionActivityQuery query, CancellationToken cancellationToken)
    {
        // Confirm the session exists in the caller's tenant (query filter applies) — prevents using this
        // endpoint to probe audit rows for another tenant's id (IDOR, NFR-SEC-007).
        if (!await db.Sessions.AnyAsync(s => s.Id == query.SessionId, cancellationToken))
            throw new NotFoundException("Session", query.SessionId);

        // AuditEntry has no global tenant filter, so scope explicitly by the caller's tenant.
        var entries = db.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == currentUser.TenantId && a.EntityId == query.SessionId);

        var total = await entries.CountAsync(cancellationToken);

        var items = await entries
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SessionActivityDto>(
            items.Select(a => a.ToActivityDto()).ToList(),
            total,
            query.Page,
            query.PageSize);
    }
}
