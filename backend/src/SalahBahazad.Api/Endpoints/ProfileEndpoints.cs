using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Application.Features.Profile.Commands.UpdateMyProfile;
using SalahBahazad.Application.Features.Profile.Queries.GetMyProfile;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Self-service profile endpoints for the signed-in staff member (Settings → Profile, FR-ADM-SET-001).
/// Authenticated-only and scoped to the caller's own record (resolved from the JWT, never a URL id),
/// so no granular permission is required and there is no IDOR surface (NFR-SEC-007).
/// </summary>
internal sealed class ProfileEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile")
            .RequireAuthorization()
            .WithTags("Profile")
            .WithOpenApi();

        group.MapGet("/", GetAsync)
            .WithName("GetMyProfile")
            .WithSummary("Get the signed-in staff member's own profile")
            .Produces<StaffDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPut("/", UpdateAsync)
            .WithName("UpdateMyProfile")
            .WithSummary("Update the signed-in staff member's own display name")
            .Produces<StaffDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetAsync(ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyProfileQuery(), cancellationToken));

    private static async Task<IResult> UpdateAsync(
        [FromBody] UpdateProfileRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new UpdateMyProfileCommand(request.DisplayName), cancellationToken));
}

/// <summary>Request body for updating the caller's own profile.</summary>
internal sealed record UpdateProfileRequest(string DisplayName);
