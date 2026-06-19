using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;
using StudentDeviceEntity = SalahBahazad.Domain.Entities.StudentDevice;
using AuditEntryEntity = SalahBahazad.Domain.Entities.AuditEntry;

namespace SalahBahazad.Application.Features.Students.DTOs;

/// <summary>A student list row for the admin triage table (FR-ADM-STU-001).</summary>
public sealed record StudentListDto(
    Guid Id,
    string FullName,
    StudentStatus Status,
    Guid GradeId,
    string? GradeName,
    string SchoolName,
    string ParentPhonePrimary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastSeenAtUtc);

/// <summary>The bound/cleared device shown on the student detail screen (FR-PLAT-DEV-006). The raw
/// device-token hash is never exposed.</summary>
public sealed record StudentDeviceDto(
    Guid Id,
    string? FingerprintSummary,
    DateTimeOffset BoundAtUtc,
    bool IsActive,
    DateTimeOffset? ClearedAtUtc,
    string? ClearReason);

/// <summary>The 360° student record for the detail screen (FR-ADM-STU-002). Exposes whether an ID
/// image exists, never the object key — the image is fetched on demand via a signed URL.</summary>
public sealed record StudentDetailDto(
    Guid Id,
    string FullName,
    StudentStatus Status,
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
    string? TermsVersion,
    DateTimeOffset? TermsAcceptedAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    StudentDeviceDto? ActiveDevice);

/// <summary>A short-lived signed URL for the student's ID-verification image (FR-PLAT-AST-003).</summary>
public sealed record StudentIdImageUrlDto(string Url, DateTimeOffset ExpiresAtUtc);

/// <summary>An audit row projected for the student history tabs (FR-ADM-STU-008).</summary>
public sealed record StudentAuditEntryDto(
    Guid Id,
    string Action,
    string? Summary,
    Guid? ActorId,
    string? ActorRole,
    string ActorType,
    string? IpAddress,
    DateTimeOffset OccurredAtUtc);

/// <summary>Result of an anonymous self-registration (FR-STU-REG-008).</summary>
public sealed record StudentRegistrationResultDto(Guid StudentId, StudentStatus Status);

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class StudentMappings
{
    public static StudentListDto ToListDto(this StudentEntity s, string? gradeName) => new(
        s.Id,
        s.FullName,
        s.Status,
        s.GradeId,
        gradeName,
        s.SchoolName,
        s.ParentPhonePrimary,
        s.CreatedAtUtc,
        s.LastSeenAtUtc);

    public static StudentDeviceDto ToDto(this StudentDeviceEntity d) => new(
        d.Id,
        d.FingerprintSummary,
        d.BoundAtUtc,
        d.IsActive,
        d.ClearedAtUtc,
        d.ClearReason);

    public static StudentDetailDto ToDetailDto(
        this StudentEntity s,
        string? gradeName,
        string? cityName,
        string? regionName,
        StudentDeviceEntity? activeDevice) => new(
        s.Id,
        s.FullName,
        s.Status,
        s.RejectionReason,
        s.GradeId,
        gradeName,
        s.CityId,
        cityName,
        s.RegionId,
        regionName,
        s.SchoolName,
        s.ParentPhonePrimary,
        s.ParentPhoneSecondary,
        !string.IsNullOrWhiteSpace(s.IdImageObjectKey),
        s.TermsVersion,
        s.TermsAcceptedAtUtc,
        s.LastSeenAtUtc,
        s.CreatedAtUtc,
        s.UpdatedAtUtc,
        activeDevice?.ToDto());

    public static StudentAuditEntryDto ToStudentAuditDto(this AuditEntryEntity a) => new(
        a.Id,
        a.Action,
        a.Summary,
        a.ActorId,
        a.ActorRole,
        a.ActorType,
        a.IpAddress,
        a.OccurredAtUtc);
}
