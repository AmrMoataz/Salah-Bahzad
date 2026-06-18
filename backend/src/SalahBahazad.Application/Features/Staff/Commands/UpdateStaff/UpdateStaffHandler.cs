using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Staff.Commands.UpdateStaff;

internal sealed class UpdateStaffHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser)
    : IRequestHandler<UpdateStaffCommand, StaffDto>
{
    public async ValueTask<StaffDto> Handle(UpdateStaffCommand command, CancellationToken cancellationToken)
    {
        // No-escalation: cannot raise a member to a role higher than the actor's own (FR-PLAT-ROLE-002).
        if (command.Role > currentUser.Role)
            throw new ForbiddenException("You cannot assign a role higher than your own.");

        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Staff member", command.Id);

        var email = command.Email.Trim().ToLowerInvariant();
        if (await db.Staff.AnyAsync(s => s.Email == email && s.Id != command.Id, cancellationToken))
            throw new ConflictException($"A staff member with the email '{email}' already exists.");

        staff.UpdateDetails(command.DisplayName, email);
        staff.UpdateRole(command.Role, currentUser.Role);
        await db.SaveChangesAsync(cancellationToken);

        return staff.ToDto();
    }
}
