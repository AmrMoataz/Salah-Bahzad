using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Staff.Commands.SetStaffActive;

internal sealed class SetStaffActiveHandler(
    IAppDbContext db,
    IFirebaseAuthService firebaseAuth,
    ICurrentUserResolver currentUser)
    : IRequestHandler<SetStaffActiveCommand, StaffDto>
{
    public async ValueTask<StaffDto> Handle(SetStaffActiveCommand command, CancellationToken cancellationToken)
    {
        if (command.Id == currentUser.UserId && !command.IsActive)
            throw new ForbiddenException("You cannot deactivate your own account.");

        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Staff member", command.Id);

        if (command.IsActive)
            staff.Activate();
        else
            staff.Deactivate();

        // DB IsActive is the authoritative gate (sign-in is rejected when inactive); mirror to Firebase.
        await db.SaveChangesAsync(cancellationToken);
        await firebaseAuth.SetUserDisabledAsync(staff.FirebaseUid, !command.IsActive, cancellationToken);

        return staff.ToDto();
    }
}
