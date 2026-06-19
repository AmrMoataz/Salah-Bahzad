using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudents;

internal sealed class ListStudentsHandler(IAppDbContext db)
    : IRequestHandler<ListStudentsQuery, PagedResult<StudentListDto>>
{
    public async ValueTask<PagedResult<StudentListDto>> Handle(
        ListStudentsQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping and soft-delete exclusion are applied automatically by the EF global query filter.
        var students = db.Students.AsNoTracking();

        if (query.Status.HasValue)
            students = students.Where(s => s.Status == query.Status.Value);

        if (query.GradeId.HasValue)
            students = students.Where(s => s.GradeId == query.GradeId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            students = students.Where(s =>
                s.FullName.ToLower().Contains(term)
                || s.SchoolName.ToLower().Contains(term)
                || s.ParentPhonePrimary.Contains(term));
        }

        var total = await students.CountAsync(cancellationToken);

        var items = await students
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Resolve grade names in one query. IgnoreQueryFilters so a row whose grade was archived
        // (soft-deleted) still shows its name rather than vanishing from the list.
        var gradeIds = items.Select(s => s.GradeId).Distinct().ToList();
        var gradeNames = await db.Grades
            .IgnoreQueryFilters()
            .Where(g => gradeIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var dtos = items
            .Select(s => s.ToListDto(gradeNames.GetValueOrDefault(s.GradeId)))
            .ToList();

        return new PagedResult<StudentListDto>(dtos, total, query.Page, query.PageSize);
    }
}
