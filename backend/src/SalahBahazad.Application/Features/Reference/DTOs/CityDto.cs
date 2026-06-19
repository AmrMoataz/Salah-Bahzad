using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Reference.DTOs;

/// <summary>
/// A city/governorate from the seeded Egypt location dataset (FR-PLAT-TAX-003). Global reference
/// data, exposed read-only and anonymously for student sign-up (FR-PLAT-TAX-005).
/// </summary>
public sealed record CityDto(Guid Id, string NameEn, string NameAr);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class CityMappings
{
    public static CityDto ToDto(this City city) => new(city.Id, city.NameEn, city.NameAr);
}
