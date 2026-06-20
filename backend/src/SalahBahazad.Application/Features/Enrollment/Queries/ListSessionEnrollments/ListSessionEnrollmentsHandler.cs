using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment.Queries.ListSessionEnrollments;

internal sealed class ListSessionEnrollmentsHandler(IAppDbContext db)
    : IRequestHandler<ListSessionEnrollmentsQuery, PagedResult<EnrollmentListDto>>
{
    public async ValueTask<PagedResult<EnrollmentListDto>> Handle(
        ListSessionEnrollmentsQuery query, CancellationToken cancellationToken)
    {
        // Tenant scoping + soft-delete exclusion are automatic (EF global query filter).
        var enrollments = db.Enrollments
            .AsNoTracking()
            .Where(e => e.SessionId == query.SessionId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            enrollments = enrollments.Where(e =>
                db.Students.Any(s => s.Id == e.StudentId && s.FullName.ToLower().Contains(term)));
        }

        var total = await enrollments.CountAsync(cancellationToken);

        var items = await enrollments
            .OrderByDescending(e => e.EnrolledAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var studentIds = items.Select(e => e.StudentId).Distinct().ToList();
        var studentNames = studentIds.Count == 0
            ? []
            : await db.Students
                .IgnoreQueryFilters()
                .Where(s => studentIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var dtos = items
            .Select(e => e.ToListDto(studentNames.GetValueOrDefault(e.StudentId)))
            .ToList();

        return new PagedResult<EnrollmentListDto>(dtos, total, query.Page, query.PageSize);
    }
}
