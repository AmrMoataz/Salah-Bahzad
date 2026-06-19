using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateSessionDetails;

internal sealed class UpdateSessionDetailsHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<UpdateSessionDetailsCommand, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(
        UpdateSessionDetailsCommand command, CancellationToken cancellationToken)
    {
        if (!await db.Grades.AnyAsync(g => g.Id == command.GradeId, cancellationToken))
            throw new NotFoundException("Grade", command.GradeId);
        if (!await db.Specializations.AnyAsync(sp => sp.Id == command.SpecializationId, cancellationToken))
            throw new NotFoundException("Specialization", command.SpecializationId);

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);

        session.UpdateDetails(
            command.Title,
            command.Description,
            command.Price,
            command.ValidityDays,
            command.GradeId,
            command.SpecializationId);

        await db.SaveChangesAsync(cancellationToken);

        return await SessionDetailLoader.LoadAsync(db, fileStorage, session.Id, cancellationToken)
            ?? throw new NotFoundException("Session", command.Id);
    }
}
