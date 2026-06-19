using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.PublishSession;

internal sealed class PublishSessionHandler(
    IAppDbContext db, IFileStorage fileStorage, ICurrentUserResolver currentUser, ILogger<PublishSessionHandler> logger)
    : IRequestHandler<PublishSessionCommand, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(PublishSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);

        // Hard-block: a configured quiz cannot request more questions than the bank can supply (FR-ADM-QZ-002).
        if (session.QuizSetting is not null)
        {
            var eligible = await SessionDetailLoader.CountQuizEligibleAsync(db, session.Id, cancellationToken);
            if (session.QuizSetting.QuestionCount > eligible)
                throw new ConflictException(
                    $"Cannot publish: the quiz needs {session.QuizSetting.QuestionCount} eligible questions " +
                    $"but only {eligible} are available (FR-ADM-QZ-002).");
        }

        // The domain enforces the legal transition; surface an illegal one as 409 rather than 500.
        try
        {
            session.Publish();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Session {SessionId} published by {ActorId}", session.Id, currentUser.UserId);

        return await SessionDetailLoader.LoadAsync(db, fileStorage, session.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);
    }
}
