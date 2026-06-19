using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Profile.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<UpdateMyProfileCommand, StaffDto>
{
    public async ValueTask<StaffDto> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        // The caller can only edit their own record — resolved from the JWT, never a URL id.
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Profile", currentUser.UserId);

        staff.UpdateDisplayName(command.DisplayName);
        await db.SaveChangesAsync(cancellationToken);

        return staff.ToDto();
    }
}
