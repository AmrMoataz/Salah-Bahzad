using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetAssignmentBehaviour;

internal sealed class GetAssignmentBehaviourHandler(IAppDbContext db)
    : IRequestHandler<GetAssignmentBehaviourQuery, IReadOnlyList<BehaviourEventDto>>
{
    public async ValueTask<IReadOnlyList<BehaviourEventDto>> Handle(
        GetAssignmentBehaviourQuery query, CancellationToken cancellationToken)
    {
        // Resolve the (tenant-scoped) assignment for the enrollment; 404 if none.
        var assignmentId = await db.UserAssignments
            .Where(a => a.EnrollmentId == query.EnrollmentId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignmentId is null)
            throw new NotFoundException("Assignment", query.EnrollmentId);

        var events = await db.AssessmentEvents
            .AsNoTracking()
            .Where(e => e.UserAssignmentId == assignmentId.Value)
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new { e.Type, e.QuestionOrder, e.OccurredAtUtc })
            .ToListAsync(cancellationToken);

        return [.. events.Select(e => new BehaviourEventDto(
            e.Type, BehaviourLabel.For(e.Type, e.QuestionOrder), e.QuestionOrder, e.OccurredAtUtc))];
    }
}
