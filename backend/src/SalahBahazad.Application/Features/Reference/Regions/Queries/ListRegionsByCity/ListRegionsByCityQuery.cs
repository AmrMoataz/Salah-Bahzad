using Mediator;
using SalahBahazad.Application.Features.Reference.DTOs;

namespace SalahBahazad.Application.Features.Reference.Regions.Queries.ListRegionsByCity;

/// <summary>
/// Lists the regions/districts of a single city, ordered by English name (FR-PLAT-TAX-003/005).
/// A region is always chosen after its city, so the city id is required.
/// </summary>
public sealed record ListRegionsByCityQuery(Guid CityId) : IRequest<IReadOnlyList<RegionDto>>;
