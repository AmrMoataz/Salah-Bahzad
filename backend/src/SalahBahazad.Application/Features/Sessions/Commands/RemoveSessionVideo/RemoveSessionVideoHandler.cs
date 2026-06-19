using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Sessions.Commands.RemoveSessionVideo;

internal sealed class RemoveSessionVideoHandler(IAppDbContext db, IAuditWriter auditWriter)
    : IRequestHandler<RemoveSessionVideoCommand, Unit>
{
    public async ValueTask<Unit> Handle(RemoveSessionVideoCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Include(s => s.Videos)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", command.SessionId);

        if (session.Videos.All(v => v.Id != command.VideoId))
            throw new NotFoundException("Video", command.VideoId);

        var removed = session.RemoveVideo(command.VideoId);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "SessionVideoRemoved", "Session", command.SessionId, $"Video removed: {removed.Title}"),
            cancellationToken);

        return Unit.Value;
    }
}
