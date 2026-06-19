using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.ArchiveSession;

internal sealed class ArchiveSessionHandler(
    IAppDbContext db, IFileStorage fileStorage, ICurrentUserResolver currentUser, ILogger<ArchiveSessionHandler> logger)
    : IRequestHandler<ArchiveSessionCommand, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(ArchiveSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);

        try
        {
            session.Archive();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Session {SessionId} archived by {ActorId}", session.Id, currentUser.UserId);

        return await SessionDetailLoader.LoadAsync(db, fileStorage, session.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);
    }
}
