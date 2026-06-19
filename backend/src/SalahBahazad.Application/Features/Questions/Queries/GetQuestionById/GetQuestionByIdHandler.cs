using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Queries.GetQuestionById;

internal sealed class GetQuestionByIdHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<GetQuestionByIdQuery, QuestionDto>
{
    public async ValueTask<QuestionDto> Handle(GetQuestionByIdQuery query, CancellationToken cancellationToken)
    {
        var question = await db.Questions
            .AsNoTracking()
            .Include(q => q.Variations)
            .FirstOrDefaultAsync(
                q => q.Id == query.QuestionId && q.SessionId == query.SessionId, cancellationToken)
            ?? throw new NotFoundException("Question", query.QuestionId);

        return await question.ToDtoAsync(fileStorage, cancellationToken);
    }
}
