namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Loosely-typed mirrors of the student-portal S6 self-service profile API (GET + PUT /api/me/profile, contract
/// §A.1/§A.2), kept separate from the production DTOs. <c>Status</c> is a string union matching the API's
/// <c>JsonStringEnumConverter</c> ("Pending"|"Active"|"Rejected"|"Inactive"). There is deliberately <b>no</b>
/// <c>Email</c> or <c>Avatar</c> member (email is the Firebase identity, shown client-side and never returned, §C.2;
/// avatar is initials-only this slice, §F) and the bound device carries no token hash (§C.5) — the tests assert those
/// keys are absent from the raw JSON.
/// </summary>
public sealed record StudentProfileResponse(
    Guid Id,
    string Serial,
    string FullName,
    string PhoneNumber,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary,
    string SchoolName,
    Guid GradeId,
    string? GradeName,
    Guid CityId,
    string? CityName,
    Guid RegionId,
    string? RegionName,
    string Status,
    ProfileBoundDeviceResponse? BoundDevice);

public sealed record ProfileBoundDeviceResponse(string? Summary, DateTimeOffset BoundAtUtc);

/// <summary>The PUT body — the seven writable fields only (gradeId/email are not accepted, §A.2).</summary>
public sealed record UpdateMyStudentProfileBody(
    string FullName,
    string PhoneNumber,
    string SchoolName,
    Guid CityId,
    Guid RegionId,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary);
