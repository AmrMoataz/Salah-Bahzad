using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Features.Profile.Commands.UpdateMyStudentProfile;
using SalahBahazad.Application.Features.Profile.DTOs;
using SalahBahazad.Application.Features.Profile.Queries.GetMyStudentProfile;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// The student-portal self-service profile (S6, contract §A, FR-STU-PRO-001/002) — the read + update of the caller's
/// own account, mirroring the staff <see cref="ProfileEndpoints"/> (<c>/api/profile</c>) but scoped to the
/// <c>Student</c> aggregate under <c>/api/me</c>. Both routes are gated to a Student-role principal
/// (<see cref="RequireStudentExtensions"/>): anon → 401, staff → 403. The student + tenant come from the JWT; there
/// is no URL id and so no IDOR surface (the caller is always the JWT subject, NFR-SEC-007). GET is a pure read of the
/// caller's own data → not audited; PUT is a state change → audited automatically by the SaveChanges interceptor (§E).
/// </summary>
internal sealed class MeProfileEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/profile")
            .WithTags("My Profile")
            .WithOpenApi();

        group.MapGet("", GetAsync)
            .RequireStudent()
            .WithName("GetMyStudentProfile")
            .WithSummary("The signed-in student's own profile + resolved grade/city/region names + bound device")
            .Produces<StudentProfileDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPut("", UpdateAsync)
            .RequireStudent()
            .WithName("UpdateMyStudentProfile")
            .WithSummary("Update the signed-in student's own profile (grade and email are not editable)")
            .Produces<StudentProfileDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetAsync(ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyStudentProfileQuery(), cancellationToken));

    private static async Task<IResult> UpdateAsync(
        [FromBody] UpdateMyStudentProfileRequest request, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new UpdateMyStudentProfileCommand(
                request.FullName,
                request.PhoneNumber,
                request.SchoolName,
                request.CityId,
                request.RegionId,
                request.ParentPhonePrimary,
                request.ParentPhoneSecondary),
            cancellationToken));
}

/// <summary>
/// Request body for updating the caller's own profile (Student-Portal S6 §A.2) — the seven writable fields only.
/// <c>gradeId</c> (staff-managed, FR-ADM-STU-005) and email (Firebase identity, §C.2) are intentionally absent: any
/// such keys in the payload are ignored.
/// </summary>
internal sealed record UpdateMyStudentProfileRequest(
    string FullName,
    string PhoneNumber,
    string SchoolName,
    Guid CityId,
    Guid RegionId,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary);
