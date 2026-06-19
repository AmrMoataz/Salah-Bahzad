using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Queries.ListQuestions;

internal sealed class ListQuestionsHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<ListQuestionsQuery, PagedResult<QuestionDto>>
{
    public async ValueTask<PagedResult<QuestionDto>> Handle(
        ListQuestionsQuery query, CancellationToken cancellationToken)
    {
        // Confirm the session exists in the caller's tenant (query filter applies).
        if (!await db.Sessions.AnyAsync(s => s.Id == query.SessionId, cancellationToken))
            throw new NotFoundException("Session", query.SessionId);

        // Questions are tenant- and soft-delete-filtered automatically.
        var questions = db.Questions
            .AsNoTracking()
            .Where(q => q.SessionId == query.SessionId);

        var total = await questions.CountAsync(cancellationToken);

        var items = await questions
            .Include(q => q.Variations)
            .OrderBy(q => q.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = new List<QuestionDto>(items.Count);
        foreach (var question in items)
            dtos.Add(await question.ToDtoAsync(fileStorage, cancellationToken));

        return new PagedResult<QuestionDto>(dtos, total, query.Page, query.PageSize);
    }
}
