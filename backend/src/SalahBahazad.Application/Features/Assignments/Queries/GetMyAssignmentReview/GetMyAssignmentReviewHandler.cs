using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Assignments.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Assignments.Queries.GetMyAssignmentReview;

internal sealed class GetMyAssignmentReviewHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage)
    : IRequestHandler<GetMyAssignmentReviewQuery, StudentAssignmentReviewDto>
{
    public async ValueTask<StudentAssignmentReviewDto> Handle(
        GetMyAssignmentReviewQuery query, CancellationToken cancellationToken)
    {
        // IDOR (NFR-SEC-007): the caller's OWN assignment by id — a GUID in the URL is not authorization.
        // Tenant scoping is the global filter, so an unknown / another student's / another tenant's id all
        // resolve to null → an opaque 404 (never reveal existence). Owned questions/options load with the root.
        var assignment = await db.UserAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == query.AssignmentId && a.StudentId == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Assignment", query.AssignmentId);

        // The answer key is revealed only after completion (FR-STU-ASG-007, contract §B.2) — never pre-submit.
        if (assignment.Status != AssignmentStatus.Completed)
            throw new ForbiddenException(
                "Finish the assignment to see your answers and score.", "assignment_in_progress");

        // The name ignores the query filters so an archived session still resolves (mirrors the staff review).
        var sessionTitle = await db.Sessions
            .IgnoreQueryFilters()
            .Where(s => s.Id == assignment.SessionId)
            .Select(s => s.Title)
            .FirstOrDefaultAsync(cancellationToken);

        return await assignment.ToReviewDtoAsync(sessionTitle, fileStorage, cancellationToken);
    }
}
