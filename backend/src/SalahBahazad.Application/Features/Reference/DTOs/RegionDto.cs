using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Reference.DTOs;

/// <summary>
/// A region/district that depends on a parent city (FR-PLAT-TAX-003). Global reference data,
/// exposed read-only and anonymously for student sign-up (FR-PLAT-TAX-005).
/// </summary>
public sealed record RegionDto(Guid Id, Guid CityId, string NameEn, string NameAr);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class RegionMappings
{
    public static RegionDto ToDto(this Region region) => new(region.Id, region.CityId, region.NameEn, region.NameAr);
}
