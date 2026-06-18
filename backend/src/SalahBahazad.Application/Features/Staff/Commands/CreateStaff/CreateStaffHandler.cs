using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;
using StaffEntity = SalahBahazad.Domain.Entities.Staff;

namespace SalahBahazad.Application.Features.Staff.Commands.CreateStaff;

internal sealed class CreateStaffHandler(
    IAppDbContext db,
    IFirebaseAuthService firebaseAuth,
    ICurrentUserResolver currentUser,
    ILogger<CreateStaffHandler> logger)
    : IRequestHandler<CreateStaffCommand, StaffDto>
{
    public async ValueTask<StaffDto> Handle(CreateStaffCommand command, CancellationToken cancellationToken)
    {
        // No-escalation: cannot create a role higher than the actor's own (FR-PLAT-ROLE-002).
        if (command.Role > currentUser.Role)
            throw new ForbiddenException("You cannot assign a role higher than your own.");

        var email = command.Email.Trim().ToLowerInvariant();

        if (await db.Staff.AnyAsync(s => s.Email == email, cancellationToken))
            throw new ConflictException($"A staff member with the email '{email}' already exists.");

        // Provision the Firebase account first; the new member sets their password via self-service.
        var firebaseUid = await firebaseAuth.CreateUserAsync(email, command.DisplayName, cancellationToken);

        var staff = StaffEntity.Create(currentUser.TenantId, firebaseUid, command.DisplayName, email, command.Role);
        db.Staff.Add(staff);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Staff {StaffId} created with role {Role} by {ActorId}", staff.Id, staff.Role, currentUser.UserId);

        return staff.ToDto();
    }
}
