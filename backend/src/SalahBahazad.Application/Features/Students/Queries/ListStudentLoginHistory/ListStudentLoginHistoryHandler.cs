using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudentLoginHistory;

internal sealed class ListStudentLoginHistoryHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<ListStudentLoginHistoryQuery, PagedResult<StudentAuditEntryDto>>
{
    /// <summary>Audit action recorded by student sign-in (Phase 3 student-portal auth).</summary>
    private const string SignInAction = "StudentSignedIn";

    public async ValueTask<PagedResult<StudentAuditEntryDto>> Handle(
        ListStudentLoginHistoryQuery query, CancellationToken cancellationToken)
    {
        var exists = await db.Students.AnyAsync(s => s.Id == query.StudentId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Student", query.StudentId);

        // AuditEntry has no global tenant filter, so scope explicitly by the caller's tenant.
        var entries = db.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == currentUser.TenantId
                        && a.EntityId == query.StudentId
                        && a.Action == SignInAction);

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
