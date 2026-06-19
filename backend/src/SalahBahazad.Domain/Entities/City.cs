using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A city/governorate in the seeded Egypt location dataset (FR-PLAT-TAX-003). Global reference
/// data: <b>no <c>TenantId</c></b>, shared across all tenants, read-only to staff, and maintained
/// only via re-seed/migration — never through the portal. Parent of <see cref="Region"/> (cascade).
/// </summary>
public sealed class City : EntityBase
{
    private City() { }

    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;

    /// <summary>
    /// Constructs a reference city with an explicit (deterministic) id so seeded ids are stable
    /// across environments. Used only by the reference-data seed (HasData).
    /// </summary>
    public static City CreateSeed(Guid id, string nameEn, string nameAr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);

        return new City { Id = id, NameEn = nameEn.Trim(), NameAr = nameAr.Trim() };
    }
}
