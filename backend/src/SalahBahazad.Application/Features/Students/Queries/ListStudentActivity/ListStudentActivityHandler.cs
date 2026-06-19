using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudentActivity;

internal sealed class ListStudentActivityHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<ListStudentActivityQuery, PagedResult<StudentAuditEntryDto>>
{
    public async ValueTask<PagedResult<StudentAuditEntryDto>> Handle(
        ListStudentActivityQuery query, CancellationToken cancellationToken)
    {
        // Confirm the student exists in the caller's tenant (tenant filter applies) — prevents using
        // this endpoint to probe audit rows for another tenant's id (IDOR, NFR-SEC-007).
        var exists = await db.Students.AnyAsync(s => s.Id == query.StudentId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Student", query.StudentId);

        // AuditEntry has no global tenant filter, so scope explicitly by the caller's tenant.
        var entries = db.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == currentUser.TenantId && a.EntityId == query.StudentId);

        var total = await entries.CountAsync(cancellationToken);

        var items = await entries
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StudentAuditEntryDto>(
            items.Select(a => a.ToStudentAuditDto()).ToList(),
            total,
            query.Page,
            query.PageSize);
    }
}
