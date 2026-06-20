using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Codes.Commands.DeleteCode;
using SalahBahazad.Application.Features.Codes.Commands.DisableCode;
using SalahBahazad.Application.Features.Codes.Commands.EnableCode;
using SalahBahazad.Application.Features.Codes.Commands.GenerateCodeBatch;
using SalahBahazad.Application.Features.Codes.DTOs;
using SalahBahazad.Application.Features.Codes.Queries.ExportBatch;
using SalahBahazad.Application.Features.Codes.Queries.ExportCodes;
using SalahBahazad.Application.Features.Codes.Queries.ListCodes;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Code register + generation endpoints (contract §2 rows #1–7, FR-PLAT-COD-001..006). Granular permissions,
/// server-enforced via <c>RequirePermission</c> (default-deny). Generate/disable/enable/delete are audited by
/// their domain events; the two CSV exports are GET reads audited explicitly in their handlers.
/// </summary>
internal sealed class CodeEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/codes")
            .WithTags("Codes")
            .WithOpenApi();

        group.MapGet("/", ListAsync)
            .RequirePermission(Permission.CodesRead)
            .WithName("ListCodes")
            .WithSummary("List codes filtered by serial/student search, status, batch and session")
            .Produces<PagedResult<CodeListDto>>();

        group.MapPost("/batches", GenerateAsync)
            .RequirePermission(Permission.CodesGenerate)
            .WithName("GenerateCodeBatch")
            .WithSummary("Mint a batch of codes for a session (value defaults to the session price)")
            .Produces<CodeBatchDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Literal "export" cannot collide with {id:guid} (guid constraint excludes it).
        group.MapGet("/export", ExportAsync)
            .RequirePermission(Permission.CodesRead)
            .WithName("ExportCodes")
            .WithSummary("Export the filtered code set as CSV (audited)")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        group.MapGet("/batches/{batchId:guid}/export", ExportBatchAsync)
            .RequirePermission(Permission.CodesRead)
            .WithName("ExportCodeBatch")
            .WithSummary("Re-export a single batch as CSV (audited)")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/disable", DisableAsync)
            .RequirePermission(Permission.CodesDisable)
            .WithName("DisableCode")
            .WithSummary("Disable a code (409 if already used)")
            .Produces<CodeListDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/enable", EnableAsync)
            .RequirePermission(Permission.CodesDisable)
            .WithName("EnableCode")
            .WithSummary("Re-enable a disabled code (409 if already used)")
            .Produces<CodeListDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequirePermission(Permission.CodesDelete)
            .WithName("DeleteCode")
            .WithSummary("Soft-delete a code (409 if already used)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] CodeStatus? status = null,
        [FromQuery] Guid? batchId = null,
        [FromQuery] Guid? sessionId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(
            new ListCodesQuery(search, status, batchId, sessionId, page, pageSize), cancellationToken));

    private static async Task<IResult> GenerateAsync(
        [FromBody] GenerateCodesRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GenerateCodeBatchCommand(request.SessionId, request.Value, request.Quantity), cancellationToken);
        return Results.Created($"/api/codes?batchId={result.BatchId}", result);
    }

    private static async Task<IResult> ExportAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] CodeStatus? status = null,
        [FromQuery] Guid? batchId = null,
        [FromQuery] Guid? sessionId = null)
    {
        var file = await sender.Send(new ExportCodesQuery(search, status, batchId, sessionId), cancellationToken);
        return Results.File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private static async Task<IResult> ExportBatchAsync(
        Guid batchId, ISender sender, CancellationToken cancellationToken)
    {
        var file = await sender.Send(new ExportBatchQuery(batchId), cancellationToken);
        return Results.File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private static async Task<IResult> DisableAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new DisableCodeCommand(id), cancellationToken));

    private static async Task<IResult> EnableAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new EnableCodeCommand(id), cancellationToken));

    private static async Task<IResult> DeleteAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCodeCommand(id), cancellationToken);
        return Results.NoContent();
    }
}

/// <summary>Request body for minting a batch (#2). <c>Value</c> is optional — defaults to the session price.</summary>
internal sealed record GenerateCodesRequest(Guid SessionId, decimal? Value, int Quantity);
