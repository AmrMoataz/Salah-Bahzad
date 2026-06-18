using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Staff.Queries.ListStaff;

internal sealed class ListStaffHandler(IAppDbContext db)
    : IRequestHandler<ListStaffQuery, PagedResult<StaffDto>>
{
    public async ValueTask<PagedResult<StaffDto>> Handle(ListStaffQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping and soft-delete exclusion are applied automatically by the EF global query filter.
        var staff = db.Staff.AsNoTracking();

        if (query.Role.HasValue)
            staff = staff.Where(s => s.Role == query.Role.Value);

        if (query.IsActive.HasValue)
            staff = staff.Where(s => s.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            staff = staff.Where(s => s.DisplayName.ToLower().Contains(term) || s.Email.Contains(term));
        }

        var total = await staff.CountAsync(cancellationToken);

        var items = await staff
            .OrderBy(s => s.DisplayName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffDto>(
            items.Select(s => s.ToDto()).ToList(),
            total,
            query.Page,
            query.PageSize);
    }
}
