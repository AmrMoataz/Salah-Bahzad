using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.SetSessionThumbnail;

internal sealed class SetSessionThumbnailHandler(
    IAppDbContext db, IFileStorage fileStorage, ICurrentUserResolver currentUser)
    : IRequestHandler<SetSessionThumbnailCommand, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(
        SetSessionThumbnailCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);

        // Upload first so the key is only persisted once the bytes are safely stored (orphan-on-failure
        // is cheap to GC; a dangling key is not) — mirrors the Phase 2 registration upload.
        var objectKey = StorageKeys.SessionThumbnail(currentUser.TenantId, command.ContentType);
        await fileStorage.UploadPrivateAsync(objectKey, command.Content, command.ContentType, cancellationToken);

        session.SetThumbnail(objectKey);
        await db.SaveChangesAsync(cancellationToken);

        return await SessionDetailLoader.LoadAsync(db, fileStorage, session.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);
    }
}
