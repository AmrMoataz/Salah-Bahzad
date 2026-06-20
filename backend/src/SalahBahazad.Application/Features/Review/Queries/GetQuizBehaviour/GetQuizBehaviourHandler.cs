using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetQuizBehaviour;

internal sealed class GetQuizBehaviourHandler(IAppDbContext db)
    : IRequestHandler<GetQuizBehaviourQuery, IReadOnlyList<BehaviourEventDto>>
{
    public async ValueTask<IReadOnlyList<BehaviourEventDto>> Handle(
        GetQuizBehaviourQuery query, CancellationToken cancellationToken)
    {
        // Resolve the (tenant-scoped) quiz for the enrollment; 404 if none. Owned attempts load with the root.
        var quiz = await db.UserQuizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.EnrollmentId == query.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("Quiz", query.EnrollmentId);

        var attemptIds = quiz.Attempts.Select(a => a.Id).ToList();
        if (attemptIds.Count == 0)
            return [];

        var events = await db.AssessmentEvents
            .AsNoTracking()
            .Where(e => e.QuizAttemptId != null && attemptIds.Contains(e.QuizAttemptId.Value))
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new { e.Type, e.OccurredAtUtc })
            .ToListAsync(cancellationToken);

        return [.. events.Select(e => new BehaviourEventDto(
            e.Type, BehaviourLabel.For(e.Type, null), null, e.OccurredAtUtc))];
    }
}
