using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Dashboard.DTOs;
using SalahBahazad.Application.Features.Dashboard.Queries.GetDashboard;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Operational dashboard endpoint (contract #2, FR-ADM-DASH-001..003, scrDashboard). <c>DashboardRead</c>,
/// default-deny. Tenant-scoped KPIs + the enrollments series + the 7 most recent non-sensitive activity rows.
/// </summary>
internal sealed class DashboardEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .WithOpenApi();

        group.MapGet("/", GetAsync)
            .RequirePermission(Permission.DashboardRead)
            .WithName("GetDashboard")
            .WithSummary("Dashboard KPIs, enrollments-by-day series and recent activity for a period or from-to")
            .Produces<DashboardDto>();
    }

    private static async Task<IResult> GetAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? period = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
        => Results.Ok(await sender.Send(new GetDashboardQuery(period, from, to), cancellationToken));
}
