using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Queries.ListSpecializations;

internal sealed class ListSpecializationsHandler(IAppDbContext db)
    : IRequestHandler<ListSpecializationsQuery, IReadOnlyList<SpecializationDto>>
{
    public async ValueTask<IReadOnlyList<SpecializationDto>> Handle(
        ListSpecializationsQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping and soft-delete exclusion are applied automatically by the EF global query filter.
        var specializations = db.Specializations.AsNoTracking();

        if (query.SubjectId.HasValue)
            specializations = specializations.Where(s => s.SubjectId == query.SubjectId.Value);

        var items = await specializations
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        // Resolve owning-subject names in one query (live subjects only, via the global filter).
        var subjectIds = items.Select(s => s.SubjectId).Distinct().ToList();
        var subjectNames = await db.Subjects
            .AsNoTracking()
            .Where(s => subjectIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return items
            .Select(s => s.ToDto(subjectNames.GetValueOrDefault(s.SubjectId, string.Empty)))
            .ToList();
    }
}
