using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Enrollment.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Enrollment.Commands.UnlockSession;

internal sealed class UnlockSessionHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    TimeProvider clock,
    ILogger<UnlockSessionHandler> logger)
    : IRequestHandler<UnlockSessionCommand, EnrollmentDto>
{
    public async ValueTask<EnrollmentDto> Handle(UnlockSessionCommand command, CancellationToken cancellationToken)
    {
        // Videos are needed so the enrollment can provision per-video access counters.
        var session = await db.Sessions
            .Include(s => s.Videos)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", command.SessionId);

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == command.StudentId, cancellationToken)
            ?? throw new NotFoundException("Student", command.StudentId);

        if (student.Status != StudentStatus.Active)
            throw new ConflictException("Only an active student can be granted access to a session.");

        var enrollment = await EnrollmentWorkflow.EnrollOrExtendAsync(
            db, currentUser.TenantId, session, command.StudentId, student.FullName,
            EnrollmentMethod.Unlock, codeId: null, amount: 0m, clock.GetUtcNow(), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Session {SessionId} unlocked for student {StudentId} by {ActorId}",
            command.SessionId, command.StudentId, currentUser.UserId);

        return await EnrollmentLoader.LoadDtoAsync(db, enrollment.Id, cancellationToken)
            ?? throw new NotFoundException("Enrollment", enrollment.Id);
    }
}
