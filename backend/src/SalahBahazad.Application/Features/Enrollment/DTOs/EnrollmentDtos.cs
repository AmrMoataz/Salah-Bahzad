using SalahBahazad.Domain.Enums;
using EnrollmentEntity = SalahBahazad.Domain.Entities.Enrollment;

namespace SalahBahazad.Application.Features.Enrollment.DTOs;

/// <summary>The result of an unlock (#9), refund (#10) or redeem (#12) — contract §1 EnrollmentDto.</summary>
public sealed record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    string? StudentName,
    Guid SessionId,
    string? SessionTitle,
    EnrollmentStatus Status,
    EnrollmentMethod Method,
    decimal Amount,
    Guid? CodeId,
    string? CodeSerial,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? ExpiresAtUtc);

/// <summary>A row in a session's "Enrolled students" tab (#8). Progress fields are Phase 5 placeholders (0).</summary>
public sealed record EnrollmentListDto(
    Guid EnrollmentId,
    Guid StudentId,
    string? StudentName,
    string StudentInitials,
    EnrollmentMethod Method,
    EnrollmentStatus Status,
    DateTimeOffset EnrolledAtUtc,
    int QuizBestPercent,
    int VideosWatched,
    int VideosTotal);

/// <summary>A row in a student's "Enrollments &amp; transactions" tab (#11).</summary>
public sealed record StudentEnrollmentDto(
    Guid EnrollmentId,
    Guid SessionId,
    string? SessionTitle,
    EnrollmentMethod Method,
    EnrollmentStatus Status,
    decimal Amount,
    DateTimeOffset EnrolledAtUtc,
    string? CodeSerial);

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class EnrollmentMappings
{
    public static EnrollmentDto ToDto(
        this EnrollmentEntity e, string? studentName, string? sessionTitle, string? codeSerial) => new(
        e.Id,
        e.StudentId,
        studentName,
        e.SessionId,
        sessionTitle,
        e.Status,
        e.Method,
        e.Amount,
        e.CodeId,
        codeSerial,
        e.EnrolledAtUtc,
        e.ExpiresAtUtc);

    public static EnrollmentListDto ToListDto(this EnrollmentEntity e, string? studentName) => new(
        e.Id,
        e.StudentId,
        studentName,
        Initials(studentName),
        e.Method,
        e.Status,
        e.EnrolledAtUtc,
        QuizBestPercent: 0,
        VideosWatched: 0,
        VideosTotal: 0);

    public static StudentEnrollmentDto ToStudentDto(
        this EnrollmentEntity e, string? sessionTitle, string? codeSerial) => new(
        e.Id,
        e.SessionId,
        sessionTitle,
        e.Method,
        e.Status,
        e.Amount,
        e.EnrolledAtUtc,
        codeSerial);

    /// <summary>Up-to-two-letter initials from a display name, for the avatar chip (contract §1).</summary>
    private static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant(),
        };
    }
}
