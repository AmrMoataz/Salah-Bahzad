using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Queries.ListGrades;

internal sealed class ListGradesHandler(IAppDbContext db)
    : IRequestHandler<ListGradesQuery, IReadOnlyList<GradeDto>>
{
    public async ValueTask<IReadOnlyList<GradeDto>> Handle(ListGradesQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping and soft-delete exclusion are applied automatically by the EF global query filter.
        var grades = await db.Grades
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return grades.Select(g => g.ToDto()).ToList();
    }
}
