using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment.Queries.ListStudentEnrollments;

internal sealed class ListStudentEnrollmentsHandler(IAppDbContext db)
    : IRequestHandler<ListStudentEnrollmentsQuery, PagedResult<StudentEnrollmentDto>>
{
    public async ValueTask<PagedResult<StudentEnrollmentDto>> Handle(
        ListStudentEnrollmentsQuery query, CancellationToken cancellationToken)
    {
        var enrollments = db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == query.StudentId);

        var total = await enrollments.CountAsync(cancellationToken);

        var items = await enrollments
            .OrderByDescending(e => e.EnrolledAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var sessionIds = items.Select(e => e.SessionId).Distinct().ToList();
        var sessionTitles = sessionIds.Count == 0
            ? []
            : await db.Sessions
                .IgnoreQueryFilters()
                .Where(s => sessionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Title, cancellationToken);

        var codeIds = items.Where(e => e.CodeId.HasValue).Select(e => e.CodeId!.Value).Distinct().ToList();
        var codeSerials = codeIds.Count == 0
            ? []
            : await db.Codes
                .IgnoreQueryFilters()
                .Where(c => codeIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Serial, cancellationToken);

        var dtos = items
            .Select(e => e.ToStudentDto(
                sessionTitles.GetValueOrDefault(e.SessionId),
                e.CodeId is Guid cid ? codeSerials.GetValueOrDefault(cid) : null))
            .ToList();

        return new PagedResult<StudentEnrollmentDto>(dtos, total, query.Page, query.PageSize);
    }
}
