using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Application.Features.App.DTOs;
using SalahBahazad.Application.Features.App.Queries.GetAppVersionStatus;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Anonymous, version-check endpoint for the native app (contract §F.1, FR-APP-UPD-001).
/// The app calls this on every launch to decide whether to nudge or block the student.
/// </summary>
internal sealed class AppEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app")
            .WithTags("App")
            .WithOpenApi()
            .AllowAnonymous();

        group.MapGet("/version-status", GetVersionStatusAsync)
            .WithName("GetAppVersionStatus")
            .WithSummary("Returns whether the calling app version is ok, has an update available, or is below the minimum floor")
            .Produces<AppVersionStatusDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetVersionStatusAsync(
        [AsParameters] AppVersionStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new GetAppVersionStatusQuery(request.Platform ?? string.Empty, request.Version ?? string.Empty),
            cancellationToken));
}

/// <summary>Query-string parameters for <c>GET /api/app/version-status</c>.</summary>
internal sealed record AppVersionStatusRequest(
    [FromQuery] string? Platform,
    [FromQuery] string? Version);
