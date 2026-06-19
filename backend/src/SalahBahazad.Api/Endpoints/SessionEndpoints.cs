using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Sessions.Commands.AddSessionMaterial;
using SalahBahazad.Application.Features.Sessions.Commands.AddSessionVideo;
using SalahBahazad.Application.Features.Sessions.Commands.ArchiveSession;
using SalahBahazad.Application.Features.Sessions.Commands.CreateSession;
using SalahBahazad.Application.Features.Sessions.Commands.DeleteSession;
using SalahBahazad.Application.Features.Sessions.Commands.PublishSession;
using SalahBahazad.Application.Features.Sessions.Commands.RemoveSessionMaterial;
using SalahBahazad.Application.Features.Sessions.Commands.RemoveSessionVideo;
using SalahBahazad.Application.Features.Sessions.Commands.ReorderSessionVideos;
using SalahBahazad.Application.Features.Sessions.Commands.SetPrerequisite;
using SalahBahazad.Application.Features.Sessions.Commands.SetSessionThumbnail;
using SalahBahazad.Application.Features.Sessions.Commands.UpdateQuizSettings;
using SalahBahazad.Application.Features.Sessions.Commands.UpdateSessionDetails;
using SalahBahazad.Application.Features.Sessions.Commands.UpdateSessionVideo;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Application.Features.Sessions.Queries.GetMaterialDownloadUrl;
using SalahBahazad.Application.Features.Sessions.Queries.GetSessionById;
using SalahBahazad.Application.Features.Sessions.Queries.ListSessionActivity;
using SalahBahazad.Application.Features.Sessions.Queries.ListSessions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Session authoring/catalogue endpoints (FR-ADM-SES-001..011, contract rows #1–18). Granular permissions,
/// server-enforced via <c>RequirePermission</c> (default-deny). Multipart sub-routes upload to R2; video
/// upload enqueues the (stubbed) transcode seam.
/// </summary>
internal sealed class SessionEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions")
            .WithTags("Sessions")
            .WithOpenApi();

        // ── Sessions ────────────────────────────────────────────────────────────
        group.MapGet("/", ListAsync)
            .RequirePermission(Permission.SessionsRead)
            .WithName("ListSessions")
            .WithSummary("List sessions filtered by grade/subject/status, with search and pagination")
            .Produces<PagedResult<SessionListDto>>();

        group.MapPost("/", CreateAsync)
            .RequirePermission(Permission.SessionsCreate)
            .WithName("CreateSession")
            .Produces<SessionDetailDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .RequirePermission(Permission.SessionsRead)
            .WithName("GetSession")
            .Produces<SessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequirePermission(Permission.SessionsEdit)
            .WithName("UpdateSession")
            .Produces<SessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/thumbnail", SetThumbnailAsync)
            .RequirePermission(Permission.SessionsEdit)
            .DisableAntiforgery()
            .WithName("SetSessionThumbnail")
            .WithSummary("Upload/replace the session thumbnail (multipart)")
            .Produces<SessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/prerequisite", SetPrerequisiteAsync)
            .RequirePermission(Permission.SessionsEdit)
            .WithName("SetSessionPrerequisite")
            .WithSummary("Set/clear the prerequisite session (rejects self-reference or cycles)")
            .Produces<SessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/quiz-settings", UpdateQuizSettingsAsync)
            .RequirePermission(Permission.SessionsEdit)
            .WithName("UpdateSessionQuizSettings")
            .Produces<SessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/publish", PublishAsync)
            .RequirePermission(Permission.SessionsPublish)
            .WithName("PublishSession")
            .Produces<SessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/archive", ArchiveAsync)
            .RequirePermission(Permission.SessionsPublish)
            .WithName("ArchiveSession")
            .Produces<SessionDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequirePermission(Permission.SessionsDelete)
            .WithName("DeleteSession")
            .WithSummary("Soft-delete a session")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/activity", ListActivityAsync)
            .RequirePermission(Permission.SessionsRead)
            .WithName("ListSessionActivity")
            .Produces<PagedResult<SessionActivityDto>>();

        // ── Videos ──────────────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/videos", AddVideoAsync)
            .RequirePermission(Permission.SessionsEdit)
            .DisableAntiforgery()
            .WithName("AddSessionVideo")
            .WithSummary("Upload a source video and enqueue transcoding (multipart)")
            .Produces<SessionVideoDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Literal "reorder" cannot collide with {videoId:guid} (guid constraint excludes it).
        group.MapPut("/{id:guid}/videos/reorder", ReorderVideosAsync)
            .RequirePermission(Permission.SessionsEdit)
            .WithName("ReorderSessionVideos")
            .Produces<IReadOnlyList<SessionVideoDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/videos/{videoId:guid}", UpdateVideoAsync)
            .RequirePermission(Permission.SessionsEdit)
            .DisableAntiforgery()
            .WithName("UpdateSessionVideo")
            .WithSummary("Edit video metadata and optionally replace the source (multipart)")
            .Produces<SessionVideoDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/videos/{videoId:guid}", RemoveVideoAsync)
            .RequirePermission(Permission.SessionsEdit)
            .WithName("RemoveSessionVideo")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // ── Materials ─────────────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/materials", AddMaterialAsync)
            .RequirePermission(Permission.SessionsEdit)
            .DisableAntiforgery()
            .WithName("AddSessionMaterial")
            .WithSummary("Upload a session material (multipart)")
            .Produces<SessionMaterialDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/materials/{materialId:guid}", RemoveMaterialAsync)
            .RequirePermission(Permission.SessionsEdit)
            .WithName("RemoveSessionMaterial")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/materials/{materialId:guid}/url", GetMaterialUrlAsync)
            .RequirePermission(Permission.SessionsRead)
            .WithName("GetSessionMaterialUrl")
            .WithSummary("Issue a short-lived signed URL for a material (preview/download)")
            .Produces<SignedUrlDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] Guid? gradeId = null,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] SessionStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(
            new ListSessionsQuery(search, gradeId, subjectId, status, page, pageSize), cancellationToken));

    private static async Task<IResult> CreateAsync(
        [FromBody] SaveSessionRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSessionCommand(
                request.Title, request.Description, request.Price, request.ValidityDays,
                request.GradeId, request.SpecializationId),
            cancellationToken);
        return Results.Created($"/api/sessions/{result.Id}", result);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetSessionByIdQuery(id), cancellationToken));

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] SaveSessionRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new UpdateSessionDetailsCommand(
                id, request.Title, request.Description, request.Price, request.ValidityDays,
                request.GradeId, request.SpecializationId),
            cancellationToken));

    private static async Task<IResult> SetThumbnailAsync(
        Guid id, IFormFile file, ISender sender, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return Results.Ok(await sender.Send(
            new SetSessionThumbnailCommand(id, content, file.ContentType, file.Length, file.FileName),
            cancellationToken));
    }

    private static async Task<IResult> SetPrerequisiteAsync(
        Guid id, [FromBody] SetPrerequisiteRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new SetPrerequisiteCommand(id, request.PrerequisiteSessionId), cancellationToken));

    private static async Task<IResult> UpdateQuizSettingsAsync(
        Guid id, [FromBody] QuizSettingsRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new UpdateQuizSettingsCommand(
                id, request.TimeLimitMinutes, request.QuestionCount, request.AttemptCount, request.MinPassPercent),
            cancellationToken));

    private static async Task<IResult> PublishAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new PublishSessionCommand(id), cancellationToken));

    private static async Task<IResult> ArchiveAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ArchiveSessionCommand(id), cancellationToken));

    private static async Task<IResult> DeleteAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSessionCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListActivityAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(new ListSessionActivityQuery(id, page, pageSize), cancellationToken));

    private static async Task<IResult> AddVideoAsync(
        Guid id,
        [FromForm] string title,
        [FromForm] int lengthMinutes,
        [FromForm] int accessCount,
        IFormFile file,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await sender.Send(
            new AddSessionVideoCommand(
                id, title, lengthMinutes, accessCount, content, file.ContentType, file.Length, file.FileName),
            cancellationToken);
        return Results.Created($"/api/sessions/{id}/videos/{result.Id}", result);
    }

    private static async Task<IResult> UpdateVideoAsync(
        Guid id,
        Guid videoId,
        [FromForm] string title,
        [FromForm] int lengthMinutes,
        [FromForm] int accessCount,
        ISender sender,
        CancellationToken cancellationToken,
        IFormFile? file = null)
    {
        Stream? content = file?.OpenReadStream();
        try
        {
            var result = await sender.Send(
                new UpdateSessionVideoCommand(
                    id, videoId, title, lengthMinutes, accessCount,
                    content, file?.ContentType, file?.Length, file?.FileName),
                cancellationToken);
            return Results.Ok(result);
        }
        finally
        {
            if (content is not null)
                await content.DisposeAsync();
        }
    }

    private static async Task<IResult> ReorderVideosAsync(
        Guid id, [FromBody] ReorderVideosRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new ReorderSessionVideosCommand(id, request.OrderedVideoIds), cancellationToken));

    private static async Task<IResult> RemoveVideoAsync(
        Guid id, Guid videoId, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveSessionVideoCommand(id, videoId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AddMaterialAsync(
        Guid id, IFormFile file, ISender sender, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await sender.Send(
            new AddSessionMaterialCommand(id, content, file.ContentType, file.Length, file.FileName),
            cancellationToken);
        return Results.Created($"/api/sessions/{id}/materials/{result.Id}", result);
    }

    private static async Task<IResult> RemoveMaterialAsync(
        Guid id, Guid materialId, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveSessionMaterialCommand(id, materialId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMaterialUrlAsync(
        Guid id, Guid materialId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMaterialDownloadUrlQuery(id, materialId), cancellationToken));
}

/// <summary>Request body for create/update session details (#2 / #4).</summary>
internal sealed record SaveSessionRequest(
    string Title, string? Description, decimal Price, int ValidityDays, Guid GradeId, Guid SpecializationId);

/// <summary>Request body for setting/clearing a prerequisite (#6).</summary>
internal sealed record SetPrerequisiteRequest(Guid? PrerequisiteSessionId);

/// <summary>Request body for quiz settings (#7) — mirrors QuizSettingDto.</summary>
internal sealed record QuizSettingsRequest(
    int TimeLimitMinutes, int QuestionCount, int AttemptCount, int MinPassPercent);

/// <summary>Request body for reordering videos (#14).</summary>
internal sealed record ReorderVideosRequest(IReadOnlyList<Guid> OrderedVideoIds);
