using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Quizzes.DTOs;

namespace SalahBahazad.Application.Features.Quizzes.Queries.GetMyQuiz;

internal sealed class GetMyQuizHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<GetMyQuizQuery, StudentQuizDto>
{
    public async ValueTask<StudentQuizDto> Handle(GetMyQuizQuery query, CancellationToken cancellationToken)
    {
        // IDOR (NFR-SEC-007): scope to the caller's own quiz — a GUID in the URL is not authorization. Owned
        // attempts load automatically with the root; tenant scoping is the global filter.
        var quiz = await db.UserQuizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                q => q.GatedSessionId == query.SessionId && q.StudentId == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("You have no quiz for this session.");

        return quiz.ToStudentDto();
    }
}
