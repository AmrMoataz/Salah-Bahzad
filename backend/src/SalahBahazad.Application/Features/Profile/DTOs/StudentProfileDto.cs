using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;
using StudentDeviceEntity = SalahBahazad.Domain.Entities.StudentDevice;

namespace SalahBahazad.Application.Features.Profile.DTOs;

/// <summary>
/// The signed-in student's own profile (Student-Portal S6, contract §A.1, FR-STU-PRO-001/002). Mirrors the staff
/// <c>StaffDto</c> self-profile but is scoped to the <c>Student</c> aggregate. Deliberately carries <b>no</b>
/// <c>email</c> field (email is the Firebase identity, shown client-side and never stored, §C.2) and <b>no</b>
/// <c>avatar</c> field (initials-only display this slice, §F). <see cref="GradeId"/>/<see cref="GradeName"/> are
/// display-only (grade is staff-managed, §C.1) — they are absent from the update request.
/// </summary>
public sealed record StudentProfileDto(
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
    StudentStatus Status,
    ProfileBoundDeviceDto? BoundDevice);

/// <summary>
/// The caller's active bound device, surfaced read-only on the profile (FR-STU-DEV-003, §C.5). Carries only the
/// human-readable fingerprint summary and the bind date — the raw device-token hash is <b>never</b> exposed.
/// </summary>
public sealed record ProfileBoundDeviceDto(
    string? Summary,
    DateTimeOffset BoundAtUtc);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class StudentProfileMappings
{
    /// <summary>
    /// Projects the student plus its resolved grade/city/region display names and active device into the profile
    /// DTO. The names are resolved by <c>StudentProfileLoader</c> (grade lookups ignore query filters so an archived
    /// grade still shows its name, §C.3); <paramref name="activeDevice"/> is the caller's single <c>IsActive</c>
    /// device or null when none is bound.
    /// </summary>
    public static StudentProfileDto ToProfileDto(
        this StudentEntity s,
        string? gradeName,
        string? cityName,
        string? regionName,
        StudentDeviceEntity? activeDevice) => new(
        s.Id,
        s.Serial,
        s.FullName,
        s.PhoneNumber,
        s.ParentPhonePrimary,
        s.ParentPhoneSecondary,
        s.SchoolName,
        s.GradeId,
        gradeName,
        s.CityId,
        cityName,
        s.RegionId,
        regionName,
        s.Status,
        activeDevice is null ? null : new ProfileBoundDeviceDto(activeDevice.FingerprintSummary, activeDevice.BoundAtUtc));
}
