using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Reference.DTOs;

namespace SalahBahazad.Application.Features.Reference.Cities.Queries.ListCities;

internal sealed class ListCitiesHandler(IAppDbContext db)
    : IRequestHandler<ListCitiesQuery, IReadOnlyList<CityDto>>
{
    public async ValueTask<IReadOnlyList<CityDto>> Handle(ListCitiesQuery query, CancellationToken cancellationToken)
    {
        // Cities are global reference data — no tenant filter applies. Read-only, so AsNoTracking.
        var cities = await db.Cities
            .AsNoTracking()
            .OrderBy(c => c.NameEn)
            .ToListAsync(cancellationToken);

        return cities.Select(c => c.ToDto()).ToList();
    }
}
