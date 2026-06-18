using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Staff.Commands.DeleteStaff;

internal sealed class DeleteStaffHandler(
    IAppDbContext db,
    TimeProvider clock,
    IFirebaseAuthService firebaseAuth,
    ICurrentUserResolver currentUser,
    ILogger<DeleteStaffHandler> logger)
    : IRequestHandler<DeleteStaffCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteStaffCommand command, CancellationToken cancellationToken)
    {
        if (command.Id == currentUser.UserId)
            throw new ForbiddenException("You cannot delete your own account.");

        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Staff member", command.Id);

        staff.SoftDelete(currentUser.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        // Disable the Firebase account so the removed member cannot obtain tokens.
        await firebaseAuth.SetUserDisabledAsync(staff.FirebaseUid, true, cancellationToken);

        logger.LogInformation("Staff {StaffId} soft-deleted by {ActorId}", staff.Id, currentUser.UserId);
        return Unit.Value;
    }
}
