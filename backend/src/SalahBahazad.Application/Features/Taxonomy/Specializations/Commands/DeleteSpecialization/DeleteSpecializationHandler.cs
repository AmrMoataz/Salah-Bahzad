using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.DeleteSpecialization;

internal sealed class DeleteSpecializationHandler(
    IAppDbContext db,
    TimeProvider clock,
    ICurrentUserResolver currentUser,
    ILogger<DeleteSpecializationHandler> logger)
    : IRequestHandler<DeleteSpecializationCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteSpecializationCommand command, CancellationToken cancellationToken)
    {
        var specialization = await db.Specializations.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Specialization", command.Id);

        specialization.SoftDelete(currentUser.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Specialization {SpecializationId} soft-deleted by {ActorId}", specialization.Id, currentUser.UserId);
        return Unit.Value;
    }
}
