using System.Net.Http.Json;
using FluentAssertions;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Location reference data is readable anonymously for sign-up (FR-PLAT-TAX-005): no token required,
/// the 27 seeded governorates are returned, and a city's regions are returned by id (FR-PLAT-TAX-003).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReferenceAnonymousTests(SalahBahazadApiFactory factory)
{
    [Fact]
    public async Task Cities_are_listed_anonymously()
    {
        var client = factory.CreateClient(); // no Authorization header

        var cities = await client.GetFromJsonAsync<List<CityResponse>>("/api/reference/cities", TestJson.Options);

        cities.Should().NotBeNull();
        cities!.Should().HaveCount(27); // seeded Egypt governorates
        cities.Should().Contain(c => c.NameEn == "Cairo");
        cities.Should().Contain(c => c.NameEn == "Alexandria");
    }

    [Fact]
    public async Task Regions_of_a_city_are_listed_anonymously()
    {
        var client = factory.CreateClient();

        var cities = await client.GetFromJsonAsync<List<CityResponse>>("/api/reference/cities", TestJson.Options);
        var cairo = cities!.Single(c => c.NameEn == "Cairo");

        var regions = await client.GetFromJsonAsync<List<RegionResponse>>(
            $"/api/reference/cities/{cairo.Id}/regions", TestJson.Options);

        regions.Should().NotBeNullOrEmpty();
        regions!.Should().OnlyContain(r => r.CityId == cairo.Id);
    }
}
