using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Sessions.Commands.RemoveSessionMaterial;

internal sealed class RemoveSessionMaterialHandler(IAppDbContext db, IAuditWriter auditWriter)
    : IRequestHandler<RemoveSessionMaterialCommand, Unit>
{
    public async ValueTask<Unit> Handle(RemoveSessionMaterialCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Include(s => s.Materials)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", command.SessionId);

        if (session.Materials.All(m => m.Id != command.MaterialId))
            throw new NotFoundException("Material", command.MaterialId);

        var removed = session.RemoveMaterial(command.MaterialId);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "SessionMaterialRemoved", "Session", command.SessionId, $"Material removed: {removed.FileName}"),
            cancellationToken);

        return Unit.Value;
    }
}
