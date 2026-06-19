using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMaterialDownloadUrl;

/// <summary>
/// Resolves the material through its (tenant-filtered) session so a caller cannot fetch another tenant's
/// material by id (IDOR, NFR-SEC-007), then issues a short-lived signed read URL. Materials are not minors'
/// PII, so the access is not separately audited (cf. the student ID-image flow).
/// </summary>
internal sealed class GetMaterialDownloadUrlHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<GetMaterialDownloadUrlQuery, SignedUrlDto>
{
    public async ValueTask<SignedUrlDto> Handle(
        GetMaterialDownloadUrlQuery query, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .AsNoTracking()
            .Include(s => s.Materials)
            .FirstOrDefaultAsync(s => s.Id == query.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", query.SessionId);

        var material = session.Materials.FirstOrDefault(m => m.Id == query.MaterialId)
            ?? throw new NotFoundException("Material", query.MaterialId);

        var signed = await fileStorage.GetSignedReadUrlAsync(material.ObjectKey, cancellationToken: cancellationToken);
        return new SignedUrlDto(signed.Url, signed.ExpiresAtUtc);
    }
}
