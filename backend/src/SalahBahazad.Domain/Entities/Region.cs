using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A region/district that depends on a parent <see cref="City"/> (FR-PLAT-TAX-003, cascading).
/// Part of the seeded Egypt location dataset: global reference data with <b>no <c>TenantId</c></b>,
/// shared across all tenants and read-only to staff.
/// </summary>
public sealed class Region : EntityBase
{
    private Region() { }

    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;

    /// <summary>Parent city — a region is always selected after its city (FR-PLAT-TAX-003).</summary>
    public Guid CityId { get; private set; }

    /// <summary>
    /// Constructs a reference region with an explicit (deterministic) id so seeded ids are stable
    /// across environments. Used only by the reference-data seed (HasData).
    /// </summary>
    public static Region CreateSeed(Guid id, Guid cityId, string nameEn, string nameAr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        if (cityId == Guid.Empty)
            throw new ArgumentException("A region must belong to a city.", nameof(cityId));

        return new Region { Id = id, CityId = cityId, NameEn = nameEn.Trim(), NameAr = nameAr.Trim() };
    }
}
