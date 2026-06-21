using Mediator;
using SalahBahazad.Application.Features.Reference.Cities.Queries.ListCities;
using SalahBahazad.Application.Features.Reference.DTOs;
using SalahBahazad.Application.Features.Reference.Grades.Queries.ListGradesForRegistration;
using SalahBahazad.Application.Features.Reference.Regions.Queries.ListRegionsByCity;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Global Egypt location reference data — read-only and <b>anonymous</b> so the student sign-up form
/// can populate its city/region pickers before a session exists (FR-PLAT-TAX-003/005). No
/// <c>RequirePermission</c>: these endpoints expose only non-sensitive, shared reference rows.
/// </summary>
internal sealed class ReferenceEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reference")
            .WithTags("Reference")
            .WithOpenApi();

        group.MapGet("/cities", ListCitiesAsync)
            .AllowAnonymous()
            .WithName("ListCities")
            .WithSummary("List all Egypt cities/governorates (anonymous, for sign-up)")
            .Produces<IReadOnlyList<CityDto>>();

        group.MapGet("/cities/{cityId:guid}/regions", ListRegionsAsync)
            .AllowAnonymous()
            .WithName("ListRegionsByCity")
            .WithSummary("List the regions/districts of a city (anonymous, for sign-up)")
            .Produces<IReadOnlyList<RegionDto>>();

        group.MapGet("/grades", ListGradesAsync)
            .AllowAnonymous()
            .WithName("ListGradesForRegistration")
            .WithSummary("List a tenant's grades by slug (anonymous, for the sign-up wizard)")
            .Produces<IReadOnlyList<GradeDto>>();
    }

    private static async Task<IResult> ListCitiesAsync(ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ListCitiesQuery(), cancellationToken));

    private static async Task<IResult> ListRegionsAsync(
        Guid cityId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ListRegionsByCityQuery(cityId), cancellationToken));

    private static async Task<IResult> ListGradesAsync(
        string? tenantSlug, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ListGradesForRegistrationQuery(tenantSlug ?? string.Empty), cancellationToken));
}
