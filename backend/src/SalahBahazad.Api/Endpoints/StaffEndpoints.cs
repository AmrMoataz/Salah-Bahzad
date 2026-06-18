using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Staff.Commands.CreateStaff;
using SalahBahazad.Application.Features.Staff.Commands.DeleteStaff;
using SalahBahazad.Application.Features.Staff.Commands.SendStaffPasswordReset;
using SalahBahazad.Application.Features.Staff.Commands.SetStaffActive;
using SalahBahazad.Application.Features.Staff.Commands.UpdateStaff;
using SalahBahazad.Application.Features.Staff.DTOs;
using SalahBahazad.Application.Features.Staff.Queries.GetStaffById;
using SalahBahazad.Application.Features.Staff.Queries.ListStaff;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Staff &amp; role management endpoints (FR-ADM-STAFF-001..004). Teacher-only granular permissions,
/// server-enforced via <c>RequirePermission</c> (default-deny). All state changes are audited automatically.
/// </summary>
internal sealed class StaffEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/staff")
            .WithTags("Staff")
            .WithOpenApi();

        group.MapGet("/", ListAsync)
            .RequirePermission(Permission.StaffRead)
            .WithName("ListStaff")
            .WithSummary("List staff filtered by role/status, with search and pagination")
            .Produces<PagedResult<StaffDto>>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .RequirePermission(Permission.StaffRead)
            .WithName("GetStaff")
            .Produces<StaffDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .RequirePermission(Permission.StaffCreate)
            .WithName("CreateStaff")
            .WithSummary("Create a staff account (role no higher than the actor's own)")
            .Produces<StaffDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequirePermission(Permission.StaffEdit)
            .WithName("UpdateStaff")
            .Produces<StaffDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/active", SetActiveAsync)
            .RequirePermission(Permission.StaffDeactivate)
            .WithName("SetStaffActive")
            .WithSummary("Activate or deactivate a staff account")
            .Produces<StaffDto>()
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/password-reset", ResetPasswordAsync)
            .RequirePermission(Permission.StaffEdit)
            .WithName("SendStaffPasswordReset")
            .WithSummary("Send a Firebase password-reset email (self-service)")
            .Produces<StaffPasswordResetResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequirePermission(Permission.StaffDelete)
            .WithName("DeleteStaff")
            .WithSummary("Soft-delete a staff account (audit attribution preserved)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] StaffRole? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(
            new ListStaffQuery(search, role, isActive, page, pageSize), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStaffByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateStaffRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateStaffCommand(request.DisplayName, request.Email, request.Role), cancellationToken);
        return Results.Created($"/api/staff/{result.Id}", result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateStaffRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateStaffCommand(id, request.DisplayName, request.Email, request.Role), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> SetActiveAsync(
        Guid id,
        [FromBody] SetStaffActiveRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SetStaffActiveCommand(id, request.IsActive), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ResetPasswordAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendStaffPasswordResetCommand(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteStaffCommand(id), cancellationToken);
        return Results.NoContent();
    }
}

/// <summary>Request body for creating a staff member.</summary>
internal sealed record CreateStaffRequest(string DisplayName, string Email, StaffRole Role);

/// <summary>Request body for updating a staff member's details and role.</summary>
internal sealed record UpdateStaffRequest(string DisplayName, string Email, StaffRole Role);

/// <summary>Request body for activating/deactivating a staff member.</summary>
internal sealed record SetStaffActiveRequest(bool IsActive);
