using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListCatalogue;

/// <summary>
/// Projects the caller's published catalogue (contract §A/§C). Published-only and tenant/soft-delete scoping
/// are automatic via the EF global query filter; the student + tenant come from <see cref="ICurrentUserResolver"/>.
/// Name resolution mirrors <c>ListSessionsHandler</c> (IgnoreQueryFilters on grade/subject/spec <b>names</b> so an
/// archived taxonomy row still labels the card). The per-caller <c>enrollmentState</c> is derived from the one
/// enrollment row per session (§C.1, expiry computed from <c>ExpiresAtUtc</c> vs now), and
/// <c>prerequisiteSatisfied</c> mirrors <c>EnforcePrerequisiteGateAsync</c> exactly (§C.2).
/// </summary>
internal sealed class ListCatalogueHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock)
    : IRequestHandler<ListCatalogueQuery, IReadOnlyList<CatalogueSessionDto>>
{
    public async ValueTask<IReadOnlyList<CatalogueSessionDto>> Handle(
        ListCatalogueQuery query, CancellationToken cancellationToken)
    {
        var studentId = currentUser.UserId;
        var now = clock.GetUtcNow();

        // Published-only (FR-PLAT-SES-008); tenant + soft-delete scoping is automatic via the global query filter.
        var sessions = db.Sessions.AsNoTracking().Where(s => s.Status == SessionStatus.Published);

        if (query.GradeId.HasValue)
            sessions = sessions.Where(s => s.GradeId == query.GradeId.Value);

        if (query.SpecializationId.HasValue)
            sessions = sessions.Where(s => s.SpecializationId == query.SpecializationId.Value);

        if (query.SubjectId.HasValue)
        {
            // Subject is derived via the session's specialization (FR-PLAT-TAX-002).
            var subjectId = query.SubjectId.Value;
            sessions = sessions.Where(s =>
                db.Specializations.Any(sp => sp.Id == s.SpecializationId && sp.SubjectId == subjectId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            sessions = sessions.Where(s => s.Title.ToLower().Contains(term));
        }

        var items = await sessions
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return [];

        // Resolve display names. IgnoreQueryFilters so an archived (soft-deleted) grade/specialization still
        // shows its name rather than the card losing its label (FR-PLAT-ROLE-004).
        var gradeIds = items.Select(s => s.GradeId).Distinct().ToList();
        var gradeNames = await db.Grades
            .IgnoreQueryFilters()
            .Where(g => gradeIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var specIds = items.Select(s => s.SpecializationId).Distinct().ToList();
        var specs = await db.Specializations
            .IgnoreQueryFilters()
            .Where(sp => specIds.Contains(sp.Id))
            .Select(sp => new { sp.Id, sp.Name, sp.SubjectId })
            .ToListAsync(cancellationToken);
        var specById = specs.ToDictionary(x => x.Id);

        var subjectIds = specs.Select(x => x.SubjectId).Distinct().ToList();
        var subjectNames = await db.Subjects
            .IgnoreQueryFilters()
            .Where(su => subjectIds.Contains(su.Id))
            .ToDictionaryAsync(su => su.Id, su => su.Name, cancellationToken);

        var sessionIds = items.Select(s => s.Id).ToList();
        var videoCounts = await db.SessionVideos
            .Where(v => sessionIds.Contains(v.SessionId))
            .GroupBy(v => v.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);

        // The caller's own enrollment per catalogue session (at most one row per session, FR-PLAT-ENR-006);
        // tenant + soft-delete filtered automatically. Group defensively so a stray duplicate never throws.
        var enrollmentBySession = (await db.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && sessionIds.Contains(e.SessionId))
                .Select(e => new { e.SessionId, e.Status, e.ExpiresAtUtc })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.SessionId)
            .ToDictionary(g => g.Key, g => g.First());

        // Prerequisite badge + satisfied flag (§C.2 — same predicate the enroll gate enforces).
        var prereqIds = items
            .Where(s => s.PrerequisiteSessionId.HasValue)
            .Select(s => s.PrerequisiteSessionId!.Value)
            .Distinct()
            .ToList();

        var prereqTitles = new Dictionary<Guid, string>();
        var prereqsWithQuestions = new HashSet<Guid>();
        var completedPrereqs = new HashSet<Guid>();
        if (prereqIds.Count > 0)
        {
            // Prerequisite stays tenant- and soft-delete-filtered (it is a session in this tenant).
            prereqTitles = await db.Sessions
                .AsNoTracking()
                .Where(s => prereqIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

            prereqsWithQuestions = (await db.Questions
                    .AsNoTracking()
                    .Where(q => prereqIds.Contains(q.SessionId))
                    .Select(q => q.SessionId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            completedPrereqs = (await db.UserAssignments
                    .AsNoTracking()
                    .Where(a => a.StudentId == studentId
                                && prereqIds.Contains(a.SessionId)
                                && a.Status == AssignmentStatus.Completed)
                    .Select(a => a.SessionId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var dtos = new List<CatalogueSessionDto>(items.Count);
        foreach (var s in items)
        {
            specById.TryGetValue(s.SpecializationId, out var spec);
            var subjectName = spec is null ? null : subjectNames.GetValueOrDefault(spec.SubjectId);
            var subjectId = spec?.SubjectId ?? Guid.Empty;

            string? thumbnailUrl = null;
            if (!string.IsNullOrWhiteSpace(s.ThumbnailObjectKey))
            {
                var signed = await fileStorage.GetSignedReadUrlAsync(
                    s.ThumbnailObjectKey, cancellationToken: cancellationToken);
                thumbnailUrl = signed.Url;
            }

            string? prerequisiteTitle = null;
            var prerequisiteSatisfied = true; // vacuously true when there is no prerequisite (§C.2).
            if (s.PrerequisiteSessionId is Guid prereqId)
            {
                prerequisiteTitle = prereqTitles.GetValueOrDefault(prereqId);
                // No question bank ⇒ nothing to complete ⇒ vacuous pass; else needs a Completed assignment.
                prerequisiteSatisfied =
                    !prereqsWithQuestions.Contains(prereqId) || completedPrereqs.Contains(prereqId);
            }

            enrollmentBySession.TryGetValue(s.Id, out var enrollment);
            var (state, expiresAt) = DeriveState(enrollment?.Status, enrollment?.ExpiresAtUtc, now);

            dtos.Add(s.ToCatalogueDto(
                gradeNames.GetValueOrDefault(s.GradeId),
                subjectId,
                subjectName,
                spec?.Name,
                videoCounts.GetValueOrDefault(s.Id),
                thumbnailUrl,
                prerequisiteTitle,
                prerequisiteSatisfied,
                state,
                expiresAt));
        }

        return dtos;
    }

    /// <summary>
    /// Derives the caller's card state from their single enrollment row (§C.1). Expiry is computed from
    /// <c>ExpiresAtUtc</c> vs now — the domain never flips <c>Status</c> to <c>Expired</c>, so an active row
    /// past its expiry derives <c>Expired</c> (an unused <c>EnrollmentStatus.Expired</c> maps there too).
    /// </summary>
    private static (CatalogueEnrollmentState State, DateTimeOffset? ExpiresAtUtc) DeriveState(
        EnrollmentStatus? status, DateTimeOffset? expiresAtUtc, DateTimeOffset now)
    {
        if (status is null)
            return (CatalogueEnrollmentState.NotEnrolled, null);

        if (status == EnrollmentStatus.Refunded)
            return (CatalogueEnrollmentState.Refunded, null);

        if (status == EnrollmentStatus.Active && (expiresAtUtc is null || expiresAtUtc > now))
            return (CatalogueEnrollmentState.Enrolled, expiresAtUtc);

        return (CatalogueEnrollmentState.Expired, null);
    }
}
