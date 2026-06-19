using Mediator;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Queries.ListGrades;

/// <summary>Lists every live grade in the caller's tenant, ordered by name (FR-ADM-TAX-001).</summary>
public sealed record ListGradesQuery : IRequest<IReadOnlyList<GradeDto>>;
