using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Videos.Commands.StartVideoPlayback;
using SalahBahazad.Application.Features.Videos.DTOs;
using SalahBahazad.Application.Features.Videos.Queries.GetHlsKey;
using SalahBahazad.Application.Features.Videos.Queries.RedeemPlayback;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// The secure video-playback gate (contract §B, FR-PLAT-VID-001..007) — student-facing and backend-only this
/// engagement (no admin screen; the future student portal / native app calls these). Every route is gated to a
/// Student-role principal (<see cref="RequireStudentExtensions"/>): anon → 401, staff → 403. The student/tenant
/// are read from the JWT; handlers IDOR-check ownership through the tenant-filtered session.
/// </summary>
internal sealed class VideoEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/videos")
            .WithTags("Video playback")
            .WithOpenApi();

        group.MapPost("/{videoId:guid}/playback", StartPlaybackAsync)
            .RequireStudent()
            .WithName("StartVideoPlayback")
            .WithSummary("Gate playback: authorise, spend one view, return a one-time handoff code (never a URL)")
            .Produces<PlaybackHandoffDto>()
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/playback/redeem", RedeemAsync)
            .RequireStudent()
            .WithName("RedeemVideoPlayback")
            .WithSummary("Exchange a one-time handoff code for a per-playback signed HLS manifest")
            .Produces<PlaybackManifestDto>()
            .Produces<ProblemDetails>(StatusCodes.Status410Gone);

        group.MapGet("/{videoId:guid}/hls.key", GetKeyAsync)
            .RequireStudent()
            .WithName("GetVideoHlsKey")
            .WithSummary("The AES-128 content key for a video (re-authorised; no view spent)")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> StartPlaybackAsync(
        Guid videoId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new StartVideoPlaybackCommand(videoId), cancellationToken));

    private static async Task<IResult> RedeemAsync(
        [FromBody] RedeemPlaybackRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // The endpoint owns the HTTP request, so it derives the absolute base the handler bakes into the
        // manifest's gated key URL.
        var apiBaseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        return Results.Ok(await sender.Send(
            new RedeemPlaybackQuery(request.HandoffCode, apiBaseUrl), cancellationToken));
    }

    private static async Task<IResult> GetKeyAsync(
        Guid videoId, ISender sender, CancellationToken cancellationToken)
    {
        var key = await sender.Send(new GetHlsKeyQuery(videoId), cancellationToken);
        return Results.File(key, "application/octet-stream");
    }
}

/// <summary>Request body for redeeming a one-time playback handoff code (#2).</summary>
internal sealed record RedeemPlaybackRequest(string HandoffCode);
