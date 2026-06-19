using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.SetPrerequisite;

internal sealed class SetPrerequisiteHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<SetPrerequisiteCommand, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(
        SetPrerequisiteCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);

        if (command.PrerequisiteSessionId is Guid prerequisiteId && prerequisiteId != Guid.Empty)
        {
            // Must reference an existing session in the caller's tenant (query filter applies).
            if (!await db.Sessions.AnyAsync(s => s.Id == prerequisiteId, cancellationToken))
                throw new NotFoundException("Session", prerequisiteId);

            // Walk the prerequisite chain; reaching this session (directly or transitively) is a cycle.
            await EnsureNoCycleAsync(db, session.Id, prerequisiteId, cancellationToken);

            session.SetPrerequisite(prerequisiteId);
        }
        else
        {
            session.SetPrerequisite(null);
        }

        await db.SaveChangesAsync(cancellationToken);

        return await SessionDetailLoader.LoadAsync(db, fileStorage, session.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);
    }

    private static async Task EnsureNoCycleAsync(
        IAppDbContext db, Guid sessionId, Guid prerequisiteId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        Guid? cursor = prerequisiteId;

        while (cursor is Guid current)
        {
            if (current == sessionId)
                throw new ConflictException(
                    "Setting this prerequisite would create a cycle (FR-ADM-SES-005).");

            if (!visited.Add(current))
                break; // defensive: a pre-existing cycle elsewhere — stop walking.

            cursor = await db.Sessions
                .Where(s => s.Id == current)
                .Select(s => s.PrerequisiteSessionId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
