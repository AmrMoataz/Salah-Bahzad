using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Profile.Queries.GetMyProfile;

internal sealed class GetMyProfileHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<GetMyProfileQuery, StaffDto>
{
    public async ValueTask<StaffDto> Handle(GetMyProfileQuery query, CancellationToken cancellationToken)
    {
        // Scoped to the caller by their own id — no IDOR surface (FR-PLAT-ROLE / NFR-SEC-007).
        var staff = await db.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Profile", currentUser.UserId);

        return staff.ToDto();
    }
}
