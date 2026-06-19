using Mediator;
using SalahBahazad.Application.Features.Reference.DTOs;

namespace SalahBahazad.Application.Features.Reference.Cities.Queries.ListCities;

/// <summary>Lists every seeded Egypt city/governorate, ordered by English name (FR-PLAT-TAX-003/005).</summary>
public sealed record ListCitiesQuery : IRequest<IReadOnlyList<CityDto>>;
