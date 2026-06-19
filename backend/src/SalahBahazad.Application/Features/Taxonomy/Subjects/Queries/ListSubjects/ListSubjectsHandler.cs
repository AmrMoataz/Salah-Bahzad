using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Queries.ListSubjects;

internal sealed class ListSubjectsHandler(IAppDbContext db)
    : IRequestHandler<ListSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    public async ValueTask<IReadOnlyList<SubjectDto>> Handle(ListSubjectsQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping and soft-delete exclusion are applied automatically by the EF global query filter.
        var subjects = await db.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        // One grouped query for live-specialization counts (both queries are tenant/soft-delete filtered).
        var counts = await db.Specializations
            .AsNoTracking()
            .GroupBy(sp => sp.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubjectId, x => x.Count, cancellationToken);

        return subjects.Select(s => s.ToDto(counts.GetValueOrDefault(s.Id))).ToList();
    }
}
