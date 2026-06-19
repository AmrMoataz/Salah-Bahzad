namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the session/question API responses (kept separate from production DTOs).</summary>
public sealed record SessionListItem(
    Guid Id,
    string Title,
    string? GradeName,
    string? SubjectName,
    string? SpecializationName,
    string Status,
    int QuestionCount,
    int VideoCount,
    int EnrolledCount);

public sealed record PagedSessionResponse(List<SessionListItem> Items, int Total, int Page, int PageSize);

public sealed record SessionVideoResponse(
    Guid Id,
    string Title,
    int Order,
    int LengthMinutes,
    int AccessCount,
    string ProcessingStatus,
    DateTimeOffset CreatedAtUtc);

public sealed record SessionMaterialResponse(
    Guid Id, string FileName, string Kind, long SizeBytes, DateTimeOffset CreatedAtUtc);

public sealed record QuizSettingResponse(
    int TimeLimitMinutes, int QuestionCount, int AttemptCount, int MinPassPercent);

public sealed record SessionDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    decimal Price,
    int ValidityDays,
    string Status,
    Guid GradeId,
    string? GradeName,
    Guid SubjectId,
    string? SubjectName,
    Guid SpecializationId,
    string? SpecializationName,
    string? ThumbnailUrl,
    Guid? PrerequisiteSessionId,
    string? PrerequisiteTitle,
    QuizSettingResponse? QuizSetting,
    List<SessionVideoResponse> Videos,
    List<SessionMaterialResponse> Materials,
    int QuestionCount,
    int QuizEligibleQuestionCount,
    int EnrolledCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record SignedUrlResponse(string Url, DateTimeOffset ExpiresAtUtc);

public sealed record SessionActivityItem(
    Guid Id,
    string Action,
    string? Summary,
    Guid? ActorId,
    string? ActorRole,
    string ActorType,
    string? IpAddress,
    DateTimeOffset OccurredAtUtc);

public sealed record PagedSessionActivityResponse(
    List<SessionActivityItem> Items, int Total, int Page, int PageSize);

public sealed record OptionResponse(Guid Id, string Text, bool IsCorrect);

public sealed record VariationResponse(Guid Id, string? BodyLatex, string? ImageUrl, List<OptionResponse> Options);

public sealed record QuestionResponse(
    Guid Id,
    Guid SessionId,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    bool IsValidForQuiz,
    string? HintUrl,
    List<OptionResponse> Options,
    List<VariationResponse> Variations,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record PagedQuestionResponse(List<QuestionResponse> Items, int Total, int Page, int PageSize);

/// <summary>Request body shapes mirrored for the tests.</summary>
public sealed record SaveSessionBody(
    string Title, string? Description, decimal Price, int ValidityDays, Guid GradeId, Guid SpecializationId);

public sealed record PrerequisiteBody(Guid? PrerequisiteSessionId);

public sealed record QuizSettingsBody(int TimeLimitMinutes, int QuestionCount, int AttemptCount, int MinPassPercent);

public sealed record OptionBody(string Text, bool IsCorrect);

public sealed record SaveQuestionBody(
    string? BodyLatex, int Mark, bool IsValidForQuiz, string? HintUrl, List<OptionBody> Options,
    string? ImageBase64 = null, string? ImageContentType = null);

public sealed record SaveVariationBody(
    string? BodyLatex, List<OptionBody> Options,
    string? ImageBase64 = null, string? ImageContentType = null);
