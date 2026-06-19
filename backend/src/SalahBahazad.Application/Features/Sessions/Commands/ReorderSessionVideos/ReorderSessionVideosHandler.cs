using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.ReorderSessionVideos;

internal sealed class ReorderSessionVideosHandler(IAppDbContext db)
    : IRequestHandler<ReorderSessionVideosCommand, IReadOnlyList<SessionVideoDto>>
{
    public async ValueTask<IReadOnlyList<SessionVideoDto>> Handle(
        ReorderSessionVideosCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Include(s => s.Videos)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", command.SessionId);

        try
        {
            session.ReorderVideos(command.OrderedVideoIds);
        }
        catch (InvalidOperationException ex)
        {
            // A list that doesn't exactly match the session's videos is bad input → 400.
            throw new ValidationException(
                [new ValidationFailure(nameof(command.OrderedVideoIds), ex.Message)]);
        }

        await db.SaveChangesAsync(cancellationToken);

        return session.Videos.OrderBy(v => v.Order).Select(v => v.ToDto()).ToList();
    }
}
