using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Queries.ListCodes;

internal sealed class ListCodesHandler(IAppDbContext db)
    : IRequestHandler<ListCodesQuery, PagedResult<CodeListDto>>
{
    public async ValueTask<PagedResult<CodeListDto>> Handle(ListCodesQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping and soft-delete exclusion are applied automatically by the EF global query filter.
        var codes = CodeFilters.Apply(
            db.Codes.AsNoTracking(), db, query.Search, query.Status, query.BatchId, query.SessionId);

        var total = await codes.CountAsync(cancellationToken);

        var items = await codes
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = await CodeListProjector.ToListDtosAsync(db, items, cancellationToken);
        return new PagedResult<CodeListDto>(dtos, total, query.Page, query.PageSize);
    }
}
