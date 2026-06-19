using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.DeleteSubject;

internal sealed class DeleteSubjectHandler(
    IAppDbContext db,
    TimeProvider clock,
    ICurrentUserResolver currentUser,
    ILogger<DeleteSubjectHandler> logger)
    : IRequestHandler<DeleteSubjectCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteSubjectCommand command, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Subject", command.Id);

        // Delete-in-use guard (FR-PLAT-TAX-004): a subject with live specializations must be archived,
        // not deleted. The global filter scopes this count to live, same-tenant specializations.
        if (await db.Specializations.AnyAsync(sp => sp.SubjectId == subject.Id, cancellationToken))
            throw new ConflictException(
                "This subject has specializations and cannot be deleted. Remove its specializations first, or archive it instead.");

        subject.SoftDelete(currentUser.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Subject {SubjectId} soft-deleted by {ActorId}", subject.Id, currentUser.UserId);
        return Unit.Value;
    }
}
