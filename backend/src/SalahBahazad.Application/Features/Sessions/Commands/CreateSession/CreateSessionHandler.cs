using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Sessions.Commands.CreateSession;

internal sealed class CreateSessionHandler(
    IAppDbContext db,
    IFileStorage fileStorage,
    ICurrentUserResolver currentUser,
    ILogger<CreateSessionHandler> logger)
    : IRequestHandler<CreateSessionCommand, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(CreateSessionCommand command, CancellationToken cancellationToken)
    {
        // Grade & specialization must exist (and be live) in the caller's tenant (query filter applies).
        if (!await db.Grades.AnyAsync(g => g.Id == command.GradeId, cancellationToken))
            throw new NotFoundException("Grade", command.GradeId);
        if (!await db.Specializations.AnyAsync(sp => sp.Id == command.SpecializationId, cancellationToken))
            throw new NotFoundException("Specialization", command.SpecializationId);

        var session = Session.Create(
            currentUser.TenantId,
            command.Title,
            command.Description,
            command.Price,
            command.ValidityDays,
            command.GradeId,
            command.SpecializationId);

        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Session {SessionId} created by {ActorId}", session.Id, currentUser.UserId);

        return await SessionDetailLoader.LoadAsync(db, fileStorage, session.Id, cancellationToken)
            ?? throw new NotFoundException("Session", session.Id);
    }
}
