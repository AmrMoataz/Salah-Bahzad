using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.DeleteGrade;

internal sealed class DeleteGradeHandler(
    IAppDbContext db,
    TimeProvider clock,
    ICurrentUserResolver currentUser,
    ILogger<DeleteGradeHandler> logger)
    : IRequestHandler<DeleteGradeCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteGradeCommand command, CancellationToken cancellationToken)
    {
        var grade = await db.Grades.FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Grade", command.Id);

        // No Session/Student references exist yet, so a grade is always free to soft-delete today.
        grade.SoftDelete(currentUser.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Grade {GradeId} soft-deleted by {ActorId}", grade.Id, currentUser.UserId);
        return Unit.Value;
    }
}
