using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Attendance.DTOs;
using SalahBahazad.Application.Features.Attendance.Queries.ExportSessionAttendance;
using SalahBahazad.Application.Features.Attendance.Queries.ExportStudentAttendance;
using SalahBahazad.Application.Features.Attendance.Queries.ListSessionAttendance;
using SalahBahazad.Application.Features.Attendance.Queries.ListStudentAttendance;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Admin attendance reporting (contract §B #4–6, scrAttendance, FR-ADM-ATT-001/002/004). Reads require
/// <see cref="Permission.AttendanceRead"/>; the CSV exports require <see cref="Permission.AttendanceExport"/> and
/// are audited explicitly in their handlers (a GET the interceptor cannot capture). Default-deny, server-enforced.
/// </summary>
internal sealed class AttendanceEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .WithOpenApi();

        group.MapGet("/sessions/{sessionId:guid}", ListSessionAsync)
            .RequirePermission(Permission.AttendanceRead)
            .WithName("ListSessionAttendance")
            .WithSummary("A session's attendance matrix (one row per enrolled student)")
            .Produces<PagedResult<SessionAttendanceRowDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/students/{studentId:guid}", ListStudentAsync)
            .RequirePermission(Permission.AttendanceRead)
            .WithName("ListStudentAttendance")
            .WithSummary("A student's per-session attendance breakdown")
            .Produces<PagedResult<StudentAttendanceRowDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Literal "export" cannot collide with the parent route (extra path segment).
        group.MapGet("/sessions/{sessionId:guid}/export", ExportSessionAsync)
            .RequirePermission(Permission.AttendanceExport)
            .WithName("ExportSessionAttendance")
            .WithSummary("Export a session's attendance as CSV (audited)")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/students/{studentId:guid}/export", ExportStudentAsync)
            .RequirePermission(Permission.AttendanceExport)
            .WithName("ExportStudentAttendance")
            .WithSummary("Export a student's attendance as CSV (audited)")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListSessionAsync(
        Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(
            new ListSessionAttendanceQuery(sessionId, page, pageSize), cancellationToken));

    private static async Task<IResult> ListStudentAsync(
        Guid studentId,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(
            new ListStudentAttendanceQuery(studentId, page, pageSize), cancellationToken));

    private static async Task<IResult> ExportSessionAsync(
        Guid sessionId, ISender sender, CancellationToken cancellationToken)
    {
        var file = await sender.Send(new ExportSessionAttendanceQuery(sessionId), cancellationToken);
        return Results.File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private static async Task<IResult> ExportStudentAsync(
        Guid studentId, ISender sender, CancellationToken cancellationToken)
    {
        var file = await sender.Send(new ExportStudentAttendanceQuery(studentId), cancellationToken);
        return Results.File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}
