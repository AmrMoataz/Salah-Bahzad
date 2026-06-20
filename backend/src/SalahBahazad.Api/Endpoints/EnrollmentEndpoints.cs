using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Enrollment.Commands.RedeemCode;
using SalahBahazad.Application.Features.Enrollment.Commands.RefundEnrollment;
using SalahBahazad.Application.Features.Enrollment.Commands.UnlockSession;
using SalahBahazad.Application.Features.Enrollment.DTOs;
using SalahBahazad.Application.Features.Enrollment.Queries.ListSessionEnrollments;
using SalahBahazad.Application.Features.Enrollment.Queries.ListStudentEnrollments;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Enrollment endpoints (contract §3 rows #8–12, FR-PLAT-ENR-001..008). The staff routes hang off
/// <c>/api/sessions/{id}</c> and <c>/api/students/{id}</c> and <c>/api/enrollments</c> with granular
/// permissions; the student redeem path (#12) is gated to a Student-role principal — the one new auth touch,
/// proven by integration tests issuing a student JWT.
/// </summary>
internal sealed class EnrollmentEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        // ── On sessions: enrolled list (#8) + unlock (#9) ───────────────────────
        var sessions = app.MapGroup("/api/sessions")
            .WithTags("Enrollment")
            .WithOpenApi();

        sessions.MapGet("/{id:guid}/enrollments", ListSessionEnrollmentsAsync)
            .RequirePermission(Permission.EnrollmentsRead)
            .WithName("ListSessionEnrollments")
            .WithSummary("List a session's enrolled students")
            .Produces<PagedResult<EnrollmentListDto>>();

        sessions.MapPost("/{id:guid}/unlock", UnlockAsync)
            .RequirePermission(Permission.EnrollmentsUnlock)
            .WithName("UnlockSession")
            .WithSummary("Grant a student access to a session, bypassing code & price")
            .Produces<EnrollmentDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // ── On enrollments: refund (#10) + student redeem (#12) ─────────────────
        var enrollments = app.MapGroup("/api/enrollments")
            .WithTags("Enrollment")
            .WithOpenApi();

        enrollments.MapPost("/{id:guid}/refund", RefundAsync)
            .RequirePermission(Permission.EnrollmentsRefund)
            .WithName("RefundEnrollment")
            .WithSummary("Refund an active enrollment; returns any redeemed code to circulation")
            .Produces<EnrollmentDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // #12 — student-portal redeem. Gated to a Student-role principal (the one new auth touch).
        enrollments.MapPost("/redeem", RedeemAsync)
            .RequireStudent()
            .WithName("RedeemCode")
            .WithSummary("Student redeems a code for its session (student-role only)")
            .Produces<EnrollmentDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // ── On students: their enrollments (#11) ────────────────────────────────
        var students = app.MapGroup("/api/students")
            .WithTags("Enrollment")
            .WithOpenApi();

        students.MapGet("/{id:guid}/enrollments", ListStudentEnrollmentsAsync)
            .RequirePermission(Permission.EnrollmentsRead)
            .WithName("ListStudentEnrollments")
            .WithSummary("List a student's enrollments & transactions")
            .Produces<PagedResult<StudentEnrollmentDto>>();
    }

    private static async Task<IResult> ListSessionEnrollmentsAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(
            new ListSessionEnrollmentsQuery(id, search, page, pageSize), cancellationToken));

    private static async Task<IResult> UnlockAsync(
        Guid id, [FromBody] UnlockSessionRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UnlockSessionCommand(id, request.StudentId), cancellationToken);
        return Results.Created($"/api/enrollments/{result.Id}", result);
    }

    private static async Task<IResult> RefundAsync(
        Guid id, [FromBody] RefundEnrollmentRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new RefundEnrollmentCommand(id, request.Reason), cancellationToken));

    private static async Task<IResult> RedeemAsync(
        [FromBody] RedeemCodeRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RedeemCodeCommand(request.Serial), cancellationToken);
        return Results.Created($"/api/enrollments/{result.Id}", result);
    }

    private static async Task<IResult> ListStudentEnrollmentsAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(new ListStudentEnrollmentsQuery(id, page, pageSize), cancellationToken));
}

/// <summary>Request body for unlocking a session for a student (#9).</summary>
internal sealed record UnlockSessionRequest(Guid StudentId);

/// <summary>Request body for refunding an enrollment (#10).</summary>
internal sealed record RefundEnrollmentRequest(string? Reason);

/// <summary>Request body for a student redeeming a code (#12).</summary>
internal sealed record RedeemCodeRequest(string Serial);
