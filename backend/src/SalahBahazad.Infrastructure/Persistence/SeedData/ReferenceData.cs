using System.Text.Json;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.SeedData;

/// <summary>
/// Loads the seeded Egypt location dataset (FR-PLAT-TAX-003) from an embedded JSON resource and
/// projects it into <see cref="City"/>/<see cref="Region"/> rows for EF Core <c>HasData</c>. Cities
/// (governorate level) and Regions (their dependent districts) are global reference data — no
/// <c>TenantId</c>, shared across tenants, maintained only via re-seed/migration.
/// </summary>
public static class ReferenceData
{
    /// <summary>
    /// Constant creation stamp for every seeded row, so <c>HasData</c> values never drift between
    /// migrations (a non-constant timestamp would make EF detect a "change" on every model build).
    /// </summary>
    public static readonly DateTimeOffset SeedTimestampUtc = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<(IReadOnlyList<City> Cities, IReadOnlyList<Region> Regions)> Data = new(Load);

    public static IReadOnlyList<City> Cities => Data.Value.Cities;
    public static IReadOnlyList<Region> Regions => Data.Value.Regions;

    private static (IReadOnlyList<City>, IReadOnlyList<Region>) Load()
    {
        var assembly = typeof(ReferenceData).Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("egypt-locations.json", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Embedded resource 'egypt-locations.json' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var nodes = JsonSerializer.Deserialize<List<CityNode>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Egypt location reference data could not be parsed.");

        var cities = new List<City>(nodes.Count);
        var regions = new List<Region>();

        foreach (var node in nodes)
        {
            var cityId = DeterministicGuid.Create($"city|{node.NameEn}");
            var city = City.CreateSeed(cityId, node.NameEn, node.NameAr);
            city.CreatedAtUtc = SeedTimestampUtc;
            cities.Add(city);

            foreach (var region in node.Regions)
            {
                var regionId = DeterministicGuid.Create($"region|{node.NameEn}|{region.NameEn}");
                var entity = Region.CreateSeed(regionId, cityId, region.NameEn, region.NameAr);
                entity.CreatedAtUtc = SeedTimestampUtc;
                regions.Add(entity);
            }
        }

        return (cities, regions);
    }

    private sealed record CityNode(string NameEn, string NameAr, List<RegionNode> Regions);

    private sealed record RegionNode(string NameEn, string NameAr);
}
