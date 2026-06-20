using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Assignments.DTOs;

namespace SalahBahazad.Application.Features.Assignments.Queries.GetMyAssignment;

internal sealed class GetMyAssignmentHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage)
    : IRequestHandler<GetMyAssignmentQuery, StudentAssignmentDto>
{
    public async ValueTask<StudentAssignmentDto> Handle(
        GetMyAssignmentQuery query, CancellationToken cancellationToken)
    {
        // IDOR (NFR-SEC-007): scope to the caller's own assignment — a GUID in the URL is not authorization.
        // Owned questions/options load automatically with the root. Tenant scoping is the global filter.
        var assignment = await db.UserAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.SessionId == query.SessionId && a.StudentId == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("You have no assignment for this session.");

        return await assignment.ToStudentDtoAsync(fileStorage, cancellationToken);
    }
}
