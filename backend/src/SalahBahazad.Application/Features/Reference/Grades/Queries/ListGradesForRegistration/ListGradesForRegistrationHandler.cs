using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Reference.Grades.Queries.ListGradesForRegistration;

internal sealed class ListGradesForRegistrationHandler(IAppDbContext db)
    : IRequestHandler<ListGradesForRegistrationQuery, IReadOnlyList<GradeDto>>
{
    public async ValueTask<IReadOnlyList<GradeDto>> Handle(
        ListGradesForRegistrationQuery query, CancellationToken cancellationToken)
    {
        // Tenant is the root (no query filter); the anonymous wizard supplies its tenant slug.
        var slug = query.TenantSlug.Trim().ToLowerInvariant();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken)
            ?? throw new NotFoundException("Tenant", query.TenantSlug);

        // Anonymous: no tenant claim, so the global filter resolves to Guid.Empty and would return nothing.
        // Filter the tenant scope and soft-delete explicitly instead. Read-only, so AsNoTracking.
        var grades = await db.Grades
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(g => g.TenantId == tenant.Id && !g.IsDeleted)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return grades.Select(g => g.ToDto()).ToList();
    }
}
