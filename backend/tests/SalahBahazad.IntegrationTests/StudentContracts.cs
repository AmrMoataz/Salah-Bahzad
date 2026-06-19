namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the student API responses (kept separate from the production DTOs).</summary>
public sealed record StudentListItem(
    Guid Id, string FullName, string PhoneNumber, string Status, Guid GradeId, string? GradeName, string SchoolName, string ParentPhonePrimary);

public sealed record PagedStudentResponse(List<StudentListItem> Items, int Total, int Page, int PageSize);

public sealed record StudentDeviceItem(
    Guid Id, string? FingerprintSummary, DateTimeOffset BoundAtUtc, bool IsActive, DateTimeOffset? ClearedAtUtc, string? ClearReason);

public sealed record StudentDetailResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Status,
    string? RejectionReason,
    Guid GradeId,
    string? GradeName,
    Guid CityId,
    string? CityName,
    Guid RegionId,
    string? RegionName,
    string SchoolName,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary,
    bool HasIdImage,
    StudentDeviceItem? ActiveDevice);

public sealed record StudentIdImageUrlResponse(string Url, DateTimeOffset ExpiresAtUtc);

public sealed record StudentRegistrationResponse(Guid StudentId, string Status);
