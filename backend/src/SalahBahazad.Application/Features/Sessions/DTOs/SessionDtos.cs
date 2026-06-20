using SalahBahazad.Domain.Enums;
using SessionEntity = SalahBahazad.Domain.Entities.Session;
using SessionVideoEntity = SalahBahazad.Domain.Entities.SessionVideo;
using SessionMaterialEntity = SalahBahazad.Domain.Entities.SessionMaterial;
using QuizSettingEntity = SalahBahazad.Domain.Entities.QuizSetting;
using AuditEntryEntity = SalahBahazad.Domain.Entities.AuditEntry;

namespace SalahBahazad.Application.Features.Sessions.DTOs;

/// <summary>A catalogue row for the admin sessions table (FR-ADM-SES-001). The admin UI renders a
/// specialization-accent tile from the names + counts — no thumbnail/price in the list.</summary>
public sealed record SessionListDto(
    Guid Id,
    string Title,
    string? GradeName,
    string? SubjectName,
    string? SpecializationName,
    SessionStatus Status,
    int QuestionCount,
    int VideoCount,
    int EnrolledCount);

/// <summary>An ordered video within a session (FR-PLAT-SES-002). <c>lengthMinutes</c> is admin-entered.</summary>
public sealed record SessionVideoDto(
    Guid Id,
    string Title,
    int Order,
    int LengthMinutes,
    int AccessCount,
    VideoProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAtUtc);

/// <summary>A downloadable material (FR-PLAT-SES-003). <c>kind</c> is the upper-case extension label
/// shown in the UI ("PDF"/"PNG"/"CSV"); the bytes are fetched on demand via a signed URL.</summary>
public sealed record SessionMaterialDto(
    Guid Id,
    string FileName,
    string Kind,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc);

/// <summary>The gating-quiz knobs (FR-PLAT-SES-006); null when unset. Minutes-based.</summary>
public sealed record QuizSettingDto(
    int TimeLimitMinutes,
    int QuestionCount,
    int AttemptCount,
    int MinPassPercent);

/// <summary>The full session record for the detail/edit screens (FR-ADM-SES-007). <c>subjectId</c>/
/// <c>subjectName</c> are derived via the specialization. <c>thumbnailUrl</c> is a short-lived signed URL
/// (stored but not displayed by the admin UI). <c>enrolledCount</c> is always 0 until Phase 4.</summary>
public sealed record SessionDetailDto(
    Guid Id,
    string Title,
    string? Description,
    decimal Price,
    int ValidityDays,
    SessionStatus Status,
    Guid GradeId,
    string? GradeName,
    Guid SubjectId,
    string? SubjectName,
    Guid SpecializationId,
    string? SpecializationName,
    string? ThumbnailUrl,
    Guid? PrerequisiteSessionId,
    string? PrerequisiteTitle,
    QuizSettingDto? QuizSetting,
    IReadOnlyList<SessionVideoDto> Videos,
    IReadOnlyList<SessionMaterialDto> Materials,
    int QuestionCount,
    int QuizEligibleQuestionCount,
    int EnrolledCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>An audit row projected for the session detail Activity tab (FR-PLAT-SES-009).</summary>
public sealed record SessionActivityDto(
    Guid Id,
    string Action,
    string? Summary,
    Guid? ActorId,
    string? ActorRole,
    string ActorType,
    string? IpAddress,
    DateTimeOffset OccurredAtUtc);

/// <summary>A short-lived signed URL for an on-demand asset download (a session material).</summary>
public sealed record SignedUrlDto(string Url, DateTimeOffset ExpiresAtUtc);

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class SessionMappings
{
    public static SessionListDto ToListDto(
        this SessionEntity s,
        string? gradeName,
        string? subjectName,
        string? specializationName,
        int questionCount,
        int videoCount,
        int enrolledCount) => new(
        s.Id,
        s.Title,
        gradeName,
        subjectName,
        specializationName,
        s.Status,
        questionCount,
        videoCount,
        enrolledCount);

    public static SessionVideoDto ToDto(this SessionVideoEntity v) => new(
        v.Id, v.Title, v.Order, v.LengthMinutes, v.AccessCount, v.ProcessingStatus, v.CreatedAtUtc);

    public static SessionMaterialDto ToDto(this SessionMaterialEntity m) => new(
        m.Id, m.FileName, MaterialKind(m.FileName, m.ContentType), m.SizeBytes, m.CreatedAtUtc);

    public static QuizSettingDto ToDto(this QuizSettingEntity q) => new(
        q.TimeLimitMinutes, q.QuestionCount, q.AttemptCount, q.MinPassPercent);

    public static SessionActivityDto ToActivityDto(this AuditEntryEntity a) => new(
        a.Id, a.Action, a.Summary, a.ActorId, a.ActorRole, a.ActorType, a.IpAddress, a.OccurredAtUtc);

    public static SessionDetailDto ToDetailDto(
        this SessionEntity s,
        string? gradeName,
        Guid subjectId,
        string? subjectName,
        string? specializationName,
        string? prerequisiteTitle,
        string? thumbnailUrl,
        int questionCount,
        int quizEligibleQuestionCount,
        int enrolledCount) => new(
        s.Id,
        s.Title,
        s.Description,
        s.Price,
        s.ValidityDays,
        s.Status,
        s.GradeId,
        gradeName,
        subjectId,
        subjectName,
        s.SpecializationId,
        specializationName,
        thumbnailUrl,
        s.PrerequisiteSessionId,
        prerequisiteTitle,
        s.QuizSetting?.ToDto(),
        s.Videos.OrderBy(v => v.Order).Select(v => v.ToDto()).ToList(),
        s.Materials.OrderBy(m => m.CreatedAtUtc).Select(m => m.ToDto()).ToList(),
        questionCount,
        quizEligibleQuestionCount,
        enrolledCount,
        s.CreatedAtUtc,
        s.UpdatedAtUtc);

    /// <summary>Upper-case file-kind label for the UI, from the file extension (PDF/PNG/CSV/JPG…).</summary>
    private static string MaterialKind(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(ext))
            return ext.TrimStart('.').ToUpperInvariant();

        return contentType switch
        {
            "application/pdf" => "PDF",
            "text/csv" => "CSV",
            "image/png" => "PNG",
            "image/jpeg" => "JPG",
            _ => "FILE",
        };
    }
}
