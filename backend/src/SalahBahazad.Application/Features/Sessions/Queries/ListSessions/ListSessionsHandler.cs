using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListSessions;

internal sealed class ListSessionsHandler(IAppDbContext db)
    : IRequestHandler<ListSessionsQuery, PagedResult<SessionListDto>>
{
    public async ValueTask<PagedResult<SessionListDto>> Handle(
        ListSessionsQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping and soft-delete exclusion are applied automatically by the EF global query filter.
        var sessions = db.Sessions.AsNoTracking();

        if (query.Status.HasValue)
            sessions = sessions.Where(s => s.Status == query.Status.Value);

        if (query.GradeId.HasValue)
            sessions = sessions.Where(s => s.GradeId == query.GradeId.Value);

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

        var total = await sessions.CountAsync(cancellationToken);

        var items = await sessions
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Resolve display names. IgnoreQueryFilters so an archived (soft-deleted) grade/specialization still
        // shows its name rather than the row losing its label (FR-PLAT-ROLE-004).
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

        // Per-session stats (FR-ADM-SES-001). Question counts are tenant/soft-delete filtered automatically.
        var sessionIds = items.Select(s => s.Id).ToList();
        var videoCounts = await db.SessionVideos
            .Where(v => sessionIds.Contains(v.SessionId))
            .GroupBy(v => v.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);
        var questionCounts = await db.Questions
            .Where(q => sessionIds.Contains(q.SessionId))
            .GroupBy(q => q.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);

        var dtos = items.Select(s =>
        {
            specById.TryGetValue(s.SpecializationId, out var spec);
            var subjectName = spec is null ? null : subjectNames.GetValueOrDefault(spec.SubjectId);
            return s.ToListDto(
                gradeNames.GetValueOrDefault(s.GradeId),
                subjectName,
                spec?.Name,
                questionCounts.GetValueOrDefault(s.Id),
                videoCounts.GetValueOrDefault(s.Id));
        }).ToList();

        return new PagedResult<SessionListDto>(dtos, total, query.Page, query.PageSize);
    }
}
