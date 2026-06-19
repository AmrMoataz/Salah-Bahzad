using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Questions;

/// <summary>
/// Loads a tracked <see cref="Question"/> scoped to its owning session — the question is tenant- and
/// soft-delete-filtered (it is a root), and the <c>SessionId</c> match enforces the URL hierarchy and
/// blocks cross-session id probing (IDOR, NFR-SEC-007). Variations (and their owned options) are included;
/// the question's own owned options auto-load.
/// </summary>
internal static class QuestionLoader
{
    public static Task<Question?> LoadTrackedAsync(
        IAppDbContext db, Guid sessionId, Guid questionId, CancellationToken cancellationToken)
        => db.Questions
            .Include(q => q.Variations)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.SessionId == sessionId, cancellationToken);
}
