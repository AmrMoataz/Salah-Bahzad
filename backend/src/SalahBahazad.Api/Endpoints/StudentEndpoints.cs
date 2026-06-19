using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Students.Commands.ApproveStudent;
using SalahBahazad.Application.Features.Students.Commands.ClearStudentDevice;
using SalahBahazad.Application.Features.Students.Commands.RegisterStudent;
using SalahBahazad.Application.Features.Students.Commands.RejectStudent;
using SalahBahazad.Application.Features.Students.Commands.SetStudentActive;
using SalahBahazad.Application.Features.Students.Commands.UpdateStudentContact;
using SalahBahazad.Application.Features.Students.DTOs;
using SalahBahazad.Application.Features.Students.Queries.GetStudentById;
using SalahBahazad.Application.Features.Students.Queries.GetStudentIdImageUrl;
using SalahBahazad.Application.Features.Students.Queries.ListStudentActivity;
using SalahBahazad.Application.Features.Students.Queries.ListStudentLoginHistory;
using SalahBahazad.Application.Features.Students.Queries.ListStudents;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Student management endpoints (FR-ADM-STU-001..010, FR-PLAT-DEV-004/006). Granular permissions,
/// server-enforced via <c>RequirePermission</c> (default-deny). State changes are audited; the
/// ID-image view is audited explicitly. The self-registration endpoint is anonymous + rate-limited.
/// </summary>
internal sealed class StudentEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/students")
            .WithTags("Students")
            .WithOpenApi();

        group.MapGet("/", ListAsync)
            .RequirePermission(Permission.StudentsRead)
            .WithName("ListStudents")
            .WithSummary("List students filtered by status/grade, with search and pagination")
            .Produces<PagedResult<StudentListDto>>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .RequirePermission(Permission.StudentsRead)
            .WithName("GetStudent")
            .Produces<StudentDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/id-image", GetIdImageAsync)
            .RequirePermission(Permission.StudentsRead)
            .WithName("GetStudentIdImageUrl")
            .WithSummary("Issue a short-lived signed URL for the ID image (access is audited)")
            .Produces<StudentIdImageUrlDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/login-history", ListLoginHistoryAsync)
            .RequirePermission(Permission.StudentsRead)
            .WithName("ListStudentLoginHistory")
            .Produces<PagedResult<StudentAuditEntryDto>>();

        group.MapGet("/{id:guid}/activity", ListActivityAsync)
            .RequirePermission(Permission.StudentsRead)
            .WithName("ListStudentActivity")
            .Produces<PagedResult<StudentAuditEntryDto>>();

        group.MapPost("/{id:guid}/approve", ApproveAsync)
            .RequirePermission(Permission.StudentsApprove)
            .WithName("ApproveStudent")
            .WithSummary("Approve a pending student")
            .Produces<StudentDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reject", RejectAsync)
            .RequirePermission(Permission.StudentsReject)
            .WithName("RejectStudent")
            .WithSummary("Reject a pending student (reason required)")
            .Produces<StudentDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/active", SetActiveAsync)
            .RequirePermission(Permission.StudentsDeactivate)
            .WithName("SetStudentActive")
            .WithSummary("Deactivate or re-activate a student account")
            .Produces<StudentDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/contact", UpdateContactAsync)
            .RequirePermission(Permission.StudentsEdit)
            .WithName("UpdateStudentContact")
            .WithSummary("Update a student's grade and parent contact numbers")
            .Produces<StudentDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/clear-device", ClearDeviceAsync)
            .RequirePermission(Permission.StudentsDeviceClear)
            .WithName("ClearStudentDevice")
            .WithSummary("Clear the student's active bound device (reason required)")
            .Produces<StudentDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Anonymous self-registration (FR-STU-REG-001..008). Rate-limited; multipart for the ID image.
        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .DisableAntiforgery()
            .WithName("RegisterStudent")
            .WithSummary("Anonymous student self-registration (creates a pending student)")
            .Accepts<RegisterStudentForm>("multipart/form-data")
            .Produces<StudentRegistrationResultDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] StudentStatus? status = null,
        [FromQuery] Guid? gradeId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(
            new ListStudentsQuery(search, status, gradeId, page, pageSize), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetStudentByIdQuery(id), cancellationToken));

    private static async Task<IResult> GetIdImageAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetStudentIdImageUrlQuery(id), cancellationToken));

    private static async Task<IResult> ListLoginHistoryAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(new ListStudentLoginHistoryQuery(id, page, pageSize), cancellationToken));

    private static async Task<IResult> ListActivityAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(new ListStudentActivityQuery(id, page, pageSize), cancellationToken));

    private static async Task<IResult> ApproveAsync(Guid id, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ApproveStudentCommand(id), cancellationToken));

    private static async Task<IResult> RejectAsync(
        Guid id, [FromBody] RejectStudentRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new RejectStudentCommand(id, request.Reason), cancellationToken));

    private static async Task<IResult> SetActiveAsync(
        Guid id, [FromBody] SetStudentActiveRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new SetStudentActiveCommand(id, request.IsActive), cancellationToken));

    private static async Task<IResult> UpdateContactAsync(
        Guid id, [FromBody] UpdateStudentContactRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new UpdateStudentContactCommand(
                id, request.GradeId, request.PhoneNumber, request.ParentPhonePrimary, request.ParentPhoneSecondary),
            cancellationToken));

    private static async Task<IResult> ClearDeviceAsync(
        Guid id, [FromBody] ClearStudentDeviceRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ClearStudentDeviceCommand(id, request.Reason), cancellationToken));

    private static async Task<IResult> RegisterAsync(
        [FromForm] string firebaseIdToken,
        [FromForm] string tenantSlug,
        [FromForm] string fullName,
        [FromForm] string phoneNumber,
        [FromForm] string parentPhonePrimary,
        [FromForm] Guid gradeId,
        [FromForm] Guid cityId,
        [FromForm] Guid regionId,
        [FromForm] string schoolName,
        [FromForm] string termsVersion,
        IFormFile idImage,
        ISender sender,
        CancellationToken cancellationToken,
        [FromForm] string? parentPhoneSecondary = null)
    {
        await using var content = idImage.OpenReadStream();

        var result = await sender.Send(
            new RegisterStudentCommand(
                firebaseIdToken,
                tenantSlug,
                fullName,
                phoneNumber,
                parentPhonePrimary,
                parentPhoneSecondary,
                gradeId,
                cityId,
                regionId,
                schoolName,
                termsVersion,
                content,
                idImage.ContentType,
                idImage.Length,
                idImage.FileName),
            cancellationToken);

        return Results.Created($"/api/students/{result.StudentId}", result);
    }
}

/// <summary>Request body for rejecting a student.</summary>
internal sealed record RejectStudentRequest(string Reason);

/// <summary>Request body for activating/deactivating a student.</summary>
internal sealed record SetStudentActiveRequest(bool IsActive);

/// <summary>Request body for correcting a student's grade and contact numbers.</summary>
internal sealed record UpdateStudentContactRequest(
    Guid GradeId, string PhoneNumber, string ParentPhonePrimary, string? ParentPhoneSecondary);

/// <summary>Request body for clearing a student's bound device.</summary>
internal sealed record ClearStudentDeviceRequest(string Reason);

/// <summary>OpenAPI shape for the multipart self-registration form (FR-STU-REG-*).</summary>
internal sealed record RegisterStudentForm(
    string FirebaseIdToken,
    string TenantSlug,
    string FullName,
    string PhoneNumber,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary,
    Guid GradeId,
    Guid CityId,
    Guid RegionId,
    string SchoolName,
    string TermsVersion,
    IFormFile IdImage);
