using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.AddSessionMaterial;

internal sealed class AddSessionMaterialHandler(
    IAppDbContext db, IFileStorage fileStorage, ICurrentUserResolver currentUser, IAuditWriter auditWriter)
    : IRequestHandler<AddSessionMaterialCommand, SessionMaterialDto>
{
    public async ValueTask<SessionMaterialDto> Handle(
        AddSessionMaterialCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Include(s => s.Materials)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", command.SessionId);

        var objectKey = StorageKeys.SessionMaterial(currentUser.TenantId, command.FileName);
        await fileStorage.UploadPrivateAsync(objectKey, command.Content, command.ContentType, cancellationToken);

        var material = session.AddMaterial(command.FileName, command.ContentType, objectKey, command.Length);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "SessionMaterialAdded", "Session", session.Id, $"Material added: {material.FileName}"),
            cancellationToken);

        return material.ToDto();
    }
}
