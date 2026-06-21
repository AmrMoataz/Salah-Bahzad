namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Loosely-typed mirror of the student-portal S2 catalogue response (GET /api/me/catalogue, contract §A.2),
/// kept separate from the production <c>CatalogueSessionDto</c>. <c>EnrollmentState</c> is a string union
/// ("NotEnrolled" | "Enrolled" | "Expired" | "Refunded") matching the API's <c>JsonStringEnumConverter</c>.
/// </summary>
public sealed record CatalogueSessionResponse(
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
    string EnrollmentState,
    DateTimeOffset? EnrolledExpiresAtUtc);
