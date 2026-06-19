using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.RegisterStudent;

/// <summary>
/// Anonymous student self-registration (FR-STU-REG-001..008): verifies the caller's Firebase identity,
/// uploads the ID-verification image to private object storage, and creates a Pending student with a
/// recorded terms-acceptance consent. Transactional so the student row and its registration audit
/// entry commit together.
/// </summary>
public sealed record RegisterStudentCommand(
    string FirebaseIdToken,
    string TenantSlug,
    string FullName,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary,
    Guid GradeId,
    Guid CityId,
    Guid RegionId,
    string SchoolName,
    string TermsVersion,
    Stream IdImageContent,
    string IdImageContentType,
    long IdImageLength,
    string IdImageFileName) : IRequest<StudentRegistrationResultDto>, ITransactionalRequest
{
    /// <summary>Accepted ID-image MIME types (validated again server-side before upload).</summary>
    public static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Maximum ID-image size (5 MB) — minors' PII, kept tight (NFR-SEC / FR-PLAT-AST-001).</summary>
    public const long MaxImageBytes = 5 * 1024 * 1024;
}
