using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetAssignmentReview;

internal sealed class GetAssignmentReviewHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<GetAssignmentReviewQuery, AssignmentReviewDto>
{
    public async ValueTask<AssignmentReviewDto> Handle(
        GetAssignmentReviewQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping is the global filter; 404 when the enrollment has no assignment in the caller's tenant.
        var assignment = await db.UserAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EnrollmentId == query.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("Assignment", query.EnrollmentId);

        // Names ignore the query filters so an archived session/student still resolves (mirrors EnrollmentLoader).
        var studentName = await db.Students
            .IgnoreQueryFilters()
            .Where(s => s.Id == assignment.StudentId)
            .Select(s => s.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        var sessionTitle = await db.Sessions
            .IgnoreQueryFilters()
            .Where(s => s.Id == assignment.SessionId)
            .Select(s => s.Title)
            .FirstOrDefaultAsync(cancellationToken);

        var questions = new List<ReviewQuestionDto>();
        foreach (var q in assignment.Questions.OrderBy(q => q.Order))
        {
            var imageUrl = string.IsNullOrWhiteSpace(q.ImageObjectKey)
                ? null
                : (await fileStorage.GetSignedReadUrlAsync(q.ImageObjectKey, cancellationToken: cancellationToken)).Url;

            questions.Add(new ReviewQuestionDto(
                q.Order,
                q.BodyLatex,
                imageUrl,
                q.Mark,
                q.HintUrl,
                [.. q.Options.OrderBy(o => o.Order).Select(o => new ReviewOptionDto(o.Id, o.Order, o.Text, o.IsCorrect))],
                q.SelectedOptionId,
                q.IsCorrect));
        }

        // Computed from current answers so an in-progress assignment shows its partial standing.
        var correctCount = assignment.Questions.Count(q => q.IsCorrect);
        var scoreMarks = assignment.Questions.Where(q => q.IsCorrect).Sum(q => q.Mark);

        return new AssignmentReviewDto(
            studentName,
            sessionTitle,
            correctCount,
            assignment.QuestionCount,
            scoreMarks,
            assignment.MaxMarks,
            assignment.Percent,
            assignment.TimeSpentSeconds,
            assignment.Status,
            questions);
    }
}
