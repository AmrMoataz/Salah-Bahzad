using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Reference.DTOs;

namespace SalahBahazad.Application.Features.Reference.Regions.Queries.ListRegionsByCity;

internal sealed class ListRegionsByCityHandler(IAppDbContext db)
    : IRequestHandler<ListRegionsByCityQuery, IReadOnlyList<RegionDto>>
{
    public async ValueTask<IReadOnlyList<RegionDto>> Handle(
        ListRegionsByCityQuery query, CancellationToken cancellationToken)
    {
        // Regions are global reference data — no tenant filter applies. Read-only, so AsNoTracking.
        var regions = await db.Regions
            .AsNoTracking()
            .Where(r => r.CityId == query.CityId)
            .OrderBy(r => r.NameEn)
            .ToListAsync(cancellationToken);

        return regions.Select(r => r.ToDto()).ToList();
    }
}
