using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Sessions.Commands.DeleteSession;

internal sealed class DeleteSessionHandler(
    IAppDbContext db, TimeProvider clock, ICurrentUserResolver currentUser, ILogger<DeleteSessionHandler> logger)
    : IRequestHandler<DeleteSessionCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);

        session.SoftDelete(currentUser.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Session {SessionId} soft-deleted by {ActorId}", session.Id, currentUser.UserId);
        return Unit.Value;
    }
}
