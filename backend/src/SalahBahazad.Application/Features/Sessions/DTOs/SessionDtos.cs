using SalahBahazad.Domain.Enums;
using SessionEntity = SalahBahazad.Domain.Entities.Session;
using SessionVideoEntity = SalahBahazad.Domain.Entities.SessionVideo;
using SessionMaterialEntity = SalahBahazad.Domain.Entities.SessionMaterial;
using QuizSettingEntity = SalahBahazad.Domain.Entities.QuizSetting;
using AuditEntryEntity = SalahBahazad.Domain.Entities.AuditEntry;
using UserAssignmentEntity = SalahBahazad.Domain.Entities.UserAssignment;
using UserQuizEntity = SalahBahazad.Domain.Entities.UserQuiz;

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

/// <summary>The caller's own state for a catalogue session (student S2, contract §C.1). <c>Expired</c> is
/// <b>derived</b> from <c>ExpiresAtUtc</c> vs now — the writer never flips <c>EnrollmentStatus</c> to Expired.</summary>
public enum CatalogueEnrollmentState
{
    NotEnrolled,
    Enrolled,
    Expired,
    Refunded,
}

/// <summary>A published-catalogue card for the student portal (FR-STU-CAT-001/002/004, contract §A.2). Shaped to
/// the prototype's <c>SessionThumb</c>: display fields + a prerequisite badge/flag + the caller's own enrollment
/// state. <c>subjectId</c>/<c>subjectName</c> are derived via the specialization; <c>thumbnailUrl</c> is a
/// short-lived signed R2 URL (null when no thumbnail).</summary>
public sealed record CatalogueSessionDto(
    Guid Id,
    string Title,
    string? Description,
    decimal Price,
    string? ThumbnailUrl,
    Guid GradeId,
    string? GradeName,
    Guid SubjectId,
    string? SubjectName,
    Guid SpecializationId,
    string? SpecializationName,
    int VideoCount,
    int ValidityDays,
    Guid? PrerequisiteSessionId,
    string? PrerequisiteTitle,
    bool PrerequisiteSatisfied,
    CatalogueEnrollmentState EnrollmentState,
    DateTimeOffset? EnrolledExpiresAtUtc);

/// <summary>An ordered video within a session (FR-PLAT-SES-002). <c>lengthSeconds</c> is computed by the transcode pipeline.</summary>
public sealed record SessionVideoDto(
    Guid Id,
    string Title,
    int Order,
    int LengthSeconds,
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

// ── Student portal · My sessions (S3, contract §A/§B/§E) ─────────────────────────────────────────

/// <summary>The <c>?state=</c> filter chip for the My-Sessions list (student S3, contract §A.1). The frontend
/// filters the loaded set client-side; the param exists for completeness and is honoured server-side. Parsed
/// leniently by the endpoint — an unrecognised value means "no filter".</summary>
public enum MySessionState
{
    NotStarted,
    InProgress,
    Completed,
    ExpiringSoon,
    Expired,
}

/// <summary>The caller's <b>completion</b> state for an enrolled session (contract §A.2/§E.2) — independent of
/// expiry, so the UI can pair e.g. <c>Completed</c> with an "Expired" chip. <c>Completed</c> iff every video has
/// a spent view; <c>NotStarted</c> iff none does; else <c>InProgress</c>.</summary>
public enum MySessionCompletionState
{
    NotStarted,
    InProgress,
    Completed,
}

/// <summary>One playlist video's lock badge (contract §E.3), computed in the same order the 5C gate authorises so
/// the badge <b>predicts</b> the Play result; the gate stays authoritative on Play.</summary>
public enum MyVideoLockState
{
    Playable,
    QuizLocked,
    Expired,
    Exhausted,
    NotReady,
}

/// <summary>The session's gate-banner state (contract §E.4): <c>Expired</c> if past expiry; else
/// <c>QuizRequired</c> if a gating quiz is unpassed; else <c>Open</c>.</summary>
public enum MySessionGateState
{
    Open,
    QuizRequired,
    Expired,
}

/// <summary>A My-Sessions list row (student S3, contract §A.2): the caller's enrolled session with derived
/// progress + expiry + completion state. <c>isExpired</c> is derived from <c>ExpiresAtUtc</c> vs now (the writer
/// never flips <c>EnrollmentStatus</c>); <c>thumbnailUrl</c> is a short-lived signed R2 URL (null when none).</summary>
public sealed record MySessionDto(
    Guid Id,
    Guid EnrollmentId,
    string Title,
    string? GradeName,
    string? SubjectName,
    string? SpecializationName,
    string? ThumbnailUrl,
    int VideoCount,
    int VideosWatched,
    int ProgressPercent,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    MySessionCompletionState State);

/// <summary>One playlist row for the session-detail view (contract §B.1). <c>lengthSeconds</c> is 0 until the
/// video is <c>Ready</c> (ffprobe-computed); <c>accessAllowed</c>/<c>accessRemaining</c> are the caller's own
/// per-video budget; <c>lockState</c> mirrors the 5C gate order (§E.3).</summary>
public sealed record MySessionVideoDto(
    Guid Id,
    string Title,
    int Order,
    int LengthSeconds,
    VideoProcessingStatus ProcessingStatus,
    int AccessAllowed,
    int AccessRemaining,
    MyVideoLockState LockState);

/// <summary>A session-detail material row (contract §B.1): names only — the bytes are fetched via the signed-URL
/// read (§C). <c>kind</c> is the upper-case extension label.</summary>
public sealed record MySessionMaterialDto(
    Guid Id,
    string FileName,
    string Kind,
    long SizeBytes);

/// <summary>The caller's assignment entry status for a session (contract §B.1) — reachable even when the session
/// is expired (FR-STU-SES-001). Score/correct are null until <c>Completed</c>.</summary>
public sealed record MyAssignmentStatusDto(
    Guid UserAssignmentId,
    AssignmentStatus Status,
    int? ScoreMarks,
    int MaxMarks,
    int? CorrectCount,
    int QuestionCount,
    DateTimeOffset? CompletedAtUtc);

/// <summary>The caller's gating-quiz entry status for a session (contract §B.1) — null in the parent when the
/// session is not quiz-gated. <c>attemptCount</c> is the total attempts allowed.</summary>
public sealed record MyQuizStatusDto(
    Guid UserQuizId,
    bool Passed,
    int? BestPercent,
    int MinPassPercent,
    int AttemptsUsed,
    int AttemptCount,
    int TimeLimitMinutes,
    int QuestionCount);

/// <summary>The full study view for one enrolled session (student S3, contract §B.1): header, progress, gate
/// banner, the ordered video playlist with per-video lock state, materials (names only), and the assignment +
/// quiz entry status.</summary>
public sealed record MySessionDetailDto(
    Guid Id,
    string Title,
    string? Description,
    Guid GradeId,
    string? GradeName,
    Guid SubjectId,
    string? SubjectName,
    Guid SpecializationId,
    string? SpecializationName,
    string? ThumbnailUrl,
    Guid EnrollmentId,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    int VideoCount,
    int VideosWatched,
    int ProgressPercent,
    MySessionGateState GateState,
    bool HasGatingQuiz,
    bool QuizPassed,
    int MinPassPercent,
    IReadOnlyList<MySessionVideoDto> Videos,
    IReadOnlyList<MySessionMaterialDto> Materials,
    MyAssignmentStatusDto? Assignment,
    MyQuizStatusDto? Quiz);

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
        v.Id, v.Title, v.Order, v.LengthSeconds, v.AccessCount, v.ProcessingStatus, v.CreatedAtUtc);

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

    public static CatalogueSessionDto ToCatalogueDto(
        this SessionEntity s,
        string? gradeName,
        Guid subjectId,
        string? subjectName,
        string? specializationName,
        int videoCount,
        string? thumbnailUrl,
        string? prerequisiteTitle,
        bool prerequisiteSatisfied,
        CatalogueEnrollmentState enrollmentState,
        DateTimeOffset? enrolledExpiresAtUtc) => new(
        s.Id,
        s.Title,
        s.Description,
        s.Price,
        thumbnailUrl,
        s.GradeId,
        gradeName,
        subjectId,
        subjectName,
        s.SpecializationId,
        specializationName,
        videoCount,
        s.ValidityDays,
        s.PrerequisiteSessionId,
        prerequisiteTitle,
        prerequisiteSatisfied,
        enrollmentState,
        enrolledExpiresAtUtc);

    // ── Student portal · My sessions (S3) ────────────────────────────────────────────────────────

    public static MySessionDto ToMyDto(
        this SessionEntity s,
        Guid enrollmentId,
        string? gradeName,
        string? subjectName,
        string? specializationName,
        string? thumbnailUrl,
        int videoCount,
        int videosWatched,
        int progressPercent,
        DateTimeOffset enrolledAtUtc,
        DateTimeOffset? expiresAtUtc,
        bool isExpired,
        MySessionCompletionState state) => new(
        s.Id,
        enrollmentId,
        s.Title,
        gradeName,
        subjectName,
        specializationName,
        thumbnailUrl,
        videoCount,
        videosWatched,
        progressPercent,
        enrolledAtUtc,
        expiresAtUtc,
        isExpired,
        state);

    public static MySessionVideoDto ToMyVideoDto(
        this SessionVideoEntity v, int accessAllowed, int accessRemaining, MyVideoLockState lockState) => new(
        v.Id, v.Title, v.Order, v.LengthSeconds, v.ProcessingStatus, accessAllowed, accessRemaining, lockState);

    public static MySessionMaterialDto ToMyMaterialDto(this SessionMaterialEntity m) => new(
        m.Id, m.FileName, MaterialKind(m.FileName, m.ContentType), m.SizeBytes);

    public static MyAssignmentStatusDto ToMyStatusDto(this UserAssignmentEntity a) => new(
        a.Id, a.Status, a.ScoreMarks, a.MaxMarks, a.CorrectCount, a.QuestionCount, a.CompletedAtUtc);

    public static MyQuizStatusDto ToMyStatusDto(this UserQuizEntity q) => new(
        q.Id, q.Passed, q.BestPercent, q.MinPassPercent, q.AttemptsUsed, q.AttemptCount, q.TimeLimitMinutes,
        q.QuestionCount);

    public static MySessionDetailDto ToMyDetailDto(
        this SessionEntity s,
        string? gradeName,
        Guid subjectId,
        string? subjectName,
        string? specializationName,
        string? thumbnailUrl,
        Guid enrollmentId,
        DateTimeOffset enrolledAtUtc,
        DateTimeOffset? expiresAtUtc,
        bool isExpired,
        int videoCount,
        int videosWatched,
        int progressPercent,
        MySessionGateState gateState,
        bool hasGatingQuiz,
        bool quizPassed,
        int minPassPercent,
        IReadOnlyList<MySessionVideoDto> videos,
        IReadOnlyList<MySessionMaterialDto> materials,
        MyAssignmentStatusDto? assignment,
        MyQuizStatusDto? quiz) => new(
        s.Id,
        s.Title,
        s.Description,
        s.GradeId,
        gradeName,
        subjectId,
        subjectName,
        s.SpecializationId,
        specializationName,
        thumbnailUrl,
        enrollmentId,
        enrolledAtUtc,
        expiresAtUtc,
        isExpired,
        videoCount,
        videosWatched,
        progressPercent,
        gateState,
        hasGatingQuiz,
        quizPassed,
        minPassPercent,
        videos,
        materials,
        assignment,
        quiz);

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

// ── Student portal · Home · weekly study plan (GET /api/me/plan, contract §A/§B/§E) ──────────────

/// <summary>The action type for a plan step (contract §B). <c>Redeem</c> opens the code modal (next session /
/// re-enroll after expiry); the others deep-link into the session detail where the runner/quiz live.</summary>
public enum MyPlanStepKind
{
    Quiz,
    Videos,
    Assignment,
    Redeem,
}

/// <summary>A plan step's derived completion (contract §B). There is no <c>Overdue</c> value — urgency rides
/// <see cref="MyPlanDueState"/>, which is derived purely from enrollment expiry.</summary>
public enum MyPlanStepStatus
{
    Pending,
    Completed,
}

/// <summary>The only real-deadline signal, derived from enrollment expiry alone (contract §B). <c>ExpiringSoon</c>
/// = active and <c>ExpiresAtUtc ≤ now + 14d</c>; <c>Expired</c> = past expiry and the step is incomplete.</summary>
public enum MyPlanDueState
{
    None,
    ExpiringSoon,
    Expired,
}

/// <summary>What the frontend does for a step (contract §B). <c>Navigate</c> carries an in-portal
/// <c>route</c>; <c>Redeem</c> carries none (the frontend opens <c>/redeem</c>).</summary>
public enum MyPlanActionType
{
    Navigate,
    Redeem,
}

/// <summary>A chunked/multi-item step's progress (contract §A.1): videos watched/total or assignment
/// answered/total. Null for Quiz/Redeem steps.</summary>
public sealed record MyPlanProgressDto(int Done, int Total);

/// <summary>What the frontend does for a step (contract §A.1): an in-portal route or a redeem intent — never a
/// fabricated external URL. <c>Route</c> is null when <see cref="MyPlanActionType.Redeem"/>.</summary>
public sealed record MyPlanActionDto(MyPlanActionType Type, string? Route, string Label);

/// <summary>One actionable plan step — or a completed one, kept for the "Completed" sub-list and the week bar
/// (contract §A.1). The plan is <b>derived state</b>: a step's <see cref="Status"/> is computed, never toggled by
/// the student. <see cref="DueState"/>/<see cref="ExpiresAtUtc"/> come only from the step's session expiry.</summary>
public sealed record MyPlanStepDto(
    string Key,
    MyPlanStepKind Kind,
    string Title,
    string? Subtitle,
    Guid SessionId,
    string SessionTitle,
    string? SpecializationName,
    MyPlanStepStatus Status,
    bool Blocked,
    string? BlockedReason,
    MyPlanDueState DueState,
    DateTimeOffset? ExpiresAtUtc,
    MyPlanProgressDto? Progress,
    MyPlanActionDto Action);

/// <summary>A "Recently enrolled" rail row (contract §A.1/§E.5): the UI renders "Added N days ago" client-side.</summary>
public sealed record MyPlanRecentDto(
    Guid SessionId, string Title, string? SpecializationName, DateTimeOffset EnrolledAtUtc);

/// <summary>The Home KPI cards (contract §A.1/§E.5): summed over the caller's <b>active</b> enrolled set for
/// sessions/videos/progress, and the <b>whole</b> non-refunded set for <see cref="CompletedSessions"/>.</summary>
public sealed record MyPlanKpisDto(
    int ActiveSessions,
    int VideosWatched,
    int VideosTotal,
    int OverallProgressPercent,
    int CompletedSessions);

/// <summary>The focus session (Path A, contract §A.1): the caller's most-urgent active, incomplete enrollment —
/// null in the onboarding/expired-only/all-done plans. <c>ThumbnailUrl</c> is a short-lived signed R2 URL signed
/// fresh per read (never cached, §C); <c>ExpiresInDays</c> is null when the session has no expiry.</summary>
public sealed record MyPlanFocusDto(
    Guid SessionId,
    string Title,
    string? SpecializationName,
    string? ThumbnailUrl,
    int ProgressPercent,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    int? ExpiresInDays,
    MyPlanDueState DueState);

/// <summary>The caller's current weekly study plan (student Home, contract §A.1): the ISO-week frame, the headline
/// counters, the KPI roll-up, the focus session, the gate-ordered steps (≤ 7), and the recently-enrolled rail.
/// Server-composed, Redis-cached, derived entirely from existing state — no stored plan, no fabricated deadlines.</summary>
public sealed record MyPlanDto(
    string IsoWeek,
    DateTimeOffset WeekStartUtc,
    DateTimeOffset WeekEndUtc,
    DateTimeOffset GeneratedAtUtc,
    int TotalSteps,
    int CompletedSteps,
    int OverdueSteps,
    MyPlanKpisDto Kpis,
    MyPlanFocusDto? Focus,
    IReadOnlyList<MyPlanStepDto> Steps,
    IReadOnlyList<MyPlanRecentDto> RecentlyEnrolled);
