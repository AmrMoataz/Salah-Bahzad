using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateQuizSettings;

internal sealed class UpdateQuizSettingsHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<UpdateQuizSettingsCommand, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(
        UpdateQuizSettingsCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);

        session.UpdateQuizSettings(
            command.TimeLimitMinutes, command.QuestionCount, command.AttemptCount, command.MinPassPercent);
        await db.SaveChangesAsync(cancellationToken);

        return await SessionDetailLoader.LoadAsync(db, fileStorage, session.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);
    }
}
