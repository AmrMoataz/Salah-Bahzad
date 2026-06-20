namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the Phase 4 code/enrollment API responses (kept separate from prod DTOs).</summary>
public sealed record CodeListItem(
    Guid Id,
    string Serial,
    decimal Value,
    string Status,
    Guid BatchId,
    string? BatchLabel,
    Guid SessionId,
    string? SessionTitle,
    Guid? RedeemedByStudentId,
    string? RedeemedByStudentName,
    DateTimeOffset? RedeemedAtUtc,
    string? CreatedByName,
    DateTimeOffset CreatedAtUtc);

public sealed record PagedCodeResponse(List<CodeListItem> Items, int Total, int Page, int PageSize);

public sealed record CodeBatchResult(
    Guid BatchId,
    string Label,
    Guid SessionId,
    string? SessionTitle,
    decimal Value,
    int Quantity,
    DateTimeOffset CreatedAtUtc);

public sealed record EnrollmentResult(
    Guid Id,
    Guid StudentId,
    string? StudentName,
    Guid SessionId,
    string? SessionTitle,
    string Status,
    string Method,
    decimal Amount,
    Guid? CodeId,
    string? CodeSerial,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record EnrollmentListItem(
    Guid EnrollmentId,
    Guid StudentId,
    string? StudentName,
    string StudentInitials,
    string Method,
    string Status,
    DateTimeOffset EnrolledAtUtc,
    int QuizBestPercent,
    int VideosWatched,
    int VideosTotal);

public sealed record PagedEnrollmentResponse(List<EnrollmentListItem> Items, int Total, int Page, int PageSize);

public sealed record StudentEnrollmentItem(
    Guid EnrollmentId,
    Guid SessionId,
    string? SessionTitle,
    string Method,
    string Status,
    decimal Amount,
    DateTimeOffset EnrolledAtUtc,
    string? CodeSerial);

public sealed record PagedStudentEnrollmentResponse(
    List<StudentEnrollmentItem> Items, int Total, int Page, int PageSize);

// ── Request bodies ──────────────────────────────────────────────────────────
public sealed record GenerateCodesRequestBody(Guid SessionId, decimal? Value, int Quantity);

public sealed record UnlockRequestBody(Guid StudentId);

public sealed record RefundRequestBody(string? Reason);

public sealed record RedeemRequestBody(string Serial);
