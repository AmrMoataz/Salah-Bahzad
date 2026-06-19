using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions;

/// <summary>
/// Loads the full <see cref="SessionDetailDto"/>, resolving the (tenant) grade/specialization and the
/// specialization's subject names, the prerequisite title, the question-bank counts, and a short-lived
/// signed thumbnail URL. Shared by the detail query and every mutation handler so they return a consistent,
/// fully-populated record. Taxonomy names ignore query filters so an archived grade/specialization still
/// shows its name (FR-PLAT-ROLE-004). Returns null when no session matches in the caller's tenant.
/// </summary>
internal static class SessionDetailLoader
{
    public static async Task<SessionDetailDto?> LoadAsync(
        IAppDbContext db, IFileStorage fileStorage, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .AsNoTracking()
            .Include(s => s.Videos)
            .Include(s => s.Materials)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return null;

        var gradeName = await db.Grades
            .IgnoreQueryFilters()
            .Where(g => g.Id == session.GradeId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var specialization = await db.Specializations
            .IgnoreQueryFilters()
            .Where(sp => sp.Id == session.SpecializationId)
            .Select(sp => new { sp.Name, sp.SubjectId })
            .FirstOrDefaultAsync(cancellationToken);

        var subjectId = specialization?.SubjectId ?? Guid.Empty;
        string? subjectName = specialization is null
            ? null
            : await db.Subjects
                .IgnoreQueryFilters()
                .Where(su => su.Id == specialization.SubjectId)
                .Select(su => su.Name)
                .FirstOrDefaultAsync(cancellationToken);

        // Prerequisite stays tenant- and soft-delete-filtered (it is a session in this tenant).
        string? prerequisiteTitle = null;
        if (session.PrerequisiteSessionId is Guid prerequisiteId)
            prerequisiteTitle = await db.Sessions
                .Where(s => s.Id == prerequisiteId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync(cancellationToken);

        var questionCount = await db.Questions
            .CountAsync(q => q.SessionId == session.Id, cancellationToken);
        var quizEligibleQuestionCount = await db.Questions
            .CountAsync(q => q.SessionId == session.Id && q.IsValidForQuiz, cancellationToken);

        string? thumbnailUrl = null;
        if (!string.IsNullOrWhiteSpace(session.ThumbnailObjectKey))
        {
            var signed = await fileStorage.GetSignedReadUrlAsync(
                session.ThumbnailObjectKey, cancellationToken: cancellationToken);
            thumbnailUrl = signed.Url;
        }

        return session.ToDetailDto(
            gradeName,
            subjectId,
            subjectName,
            specialization?.Name,
            prerequisiteTitle,
            thumbnailUrl,
            questionCount,
            quizEligibleQuestionCount);
    }

    /// <summary>Counts the session's quiz-eligible questions — the cap for quiz <c>questionCount</c>
    /// (FR-ADM-QZ-002, enforced on quiz-settings update and on publish).</summary>
    public static Task<int> CountQuizEligibleAsync(
        IAppDbContext db, Guid sessionId, CancellationToken cancellationToken)
        => db.Questions.CountAsync(q => q.SessionId == sessionId && q.IsValidForQuiz, cancellationToken);
}
