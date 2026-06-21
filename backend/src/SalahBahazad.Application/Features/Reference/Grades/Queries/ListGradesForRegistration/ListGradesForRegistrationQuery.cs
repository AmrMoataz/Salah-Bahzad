using Mediator;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Reference.Grades.Queries.ListGradesForRegistration;

/// <summary>
/// Lists the live grades of a tenant for the <b>anonymous</b> student sign-up wizard, resolving the
/// tenant by slug (the wizard has no JWT). Distinct from the staff-only Taxonomy <c>ListGradesQuery</c>,
/// which relies on the JWT-driven global filter and would return nothing here (FR-STU-REG-005).
/// </summary>
public sealed record ListGradesForRegistrationQuery(string TenantSlug) : IRequest<IReadOnlyList<GradeDto>>;
