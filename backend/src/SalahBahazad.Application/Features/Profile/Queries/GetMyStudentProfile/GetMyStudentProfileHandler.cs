using Mediator;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Profile.DTOs;

namespace SalahBahazad.Application.Features.Profile.Queries.GetMyStudentProfile;

internal sealed class GetMyStudentProfileHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<GetMyStudentProfileQuery, StudentProfileDto>
{
    public async ValueTask<StudentProfileDto> Handle(
        GetMyStudentProfileQuery query, CancellationToken cancellationToken)
    {
        // Scoped to the caller by their own id from the JWT — no URL id, no IDOR surface (NFR-SEC-007). The
        // student row is the JWT subject, so it always exists (no documented 404-self, §B); the guard is defensive.
        return await StudentProfileLoader.LoadAsync(db, currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Profile", currentUser.UserId);
    }
}
