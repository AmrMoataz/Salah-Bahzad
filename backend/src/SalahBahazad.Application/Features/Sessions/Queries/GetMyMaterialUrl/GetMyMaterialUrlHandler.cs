using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMyMaterialUrl;

/// <summary>
/// Gates a material read on the caller's own non-refunded enrollment for the session (the §C 404 boundary), then
/// resolves the material through that session and issues a short-lived signed read URL. Mirrors
/// <c>GetMaterialDownloadUrlHandler</c> but scopes by the student's enrollment instead of a staff permission;
/// materials are not minors' PII so the read is not audited. Materials stay available while the enrollment is
/// Active-but-expired (no expiry gate here, FR-STU-SES-001).
/// </summary>
internal sealed class GetMyMaterialUrlHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage)
    : IRequestHandler<GetMyMaterialUrlQuery, SignedUrlDto>
{
    public async ValueTask<SignedUrlDto> Handle(
        GetMyMaterialUrlQuery query, CancellationToken cancellationToken)
    {
        var studentId = currentUser.UserId;

        // Enrolment gate (§C): tenant + soft-delete filtered automatically; a cross-tenant/non-enrolled/refunded
        // session id finds no row → 404 (never the other tenant's material).
        var enrolled = await db.Enrollments
            .AsNoTracking()
            .AnyAsync(
                e => e.StudentId == studentId
                     && e.SessionId == query.SessionId
                     && e.Status != EnrollmentStatus.Refunded,
                cancellationToken);

        if (!enrolled)
            throw new NotFoundException("Session", query.SessionId);

        // The material must belong to that session (resolved by both ids → no IDOR via a foreign material id).
        var objectKey = await db.SessionMaterials
            .AsNoTracking()
            .Where(m => m.SessionId == query.SessionId && m.Id == query.MaterialId)
            .Select(m => m.ObjectKey)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Material", query.MaterialId);

        var signed = await fileStorage.GetSignedReadUrlAsync(objectKey, cancellationToken: cancellationToken);
        return new SignedUrlDto(signed.Url, signed.ExpiresAtUtc);
    }
}
