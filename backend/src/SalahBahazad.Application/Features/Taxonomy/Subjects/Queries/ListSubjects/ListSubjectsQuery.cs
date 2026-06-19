using Mediator;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Queries.ListSubjects;

/// <summary>
/// Lists every live subject in the caller's tenant with its live-specialization count, ordered by
/// name (FR-ADM-TAX-001). The count drives the delete-in-use affordance (FR-PLAT-TAX-004).
/// </summary>
public sealed record ListSubjectsQuery : IRequest<IReadOnlyList<SubjectDto>>;
