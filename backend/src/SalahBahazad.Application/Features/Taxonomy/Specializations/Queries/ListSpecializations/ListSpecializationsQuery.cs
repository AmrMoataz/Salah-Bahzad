using Mediator;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Queries.ListSpecializations;

/// <summary>
/// Lists live specializations in the caller's tenant, optionally filtered to a single subject
/// (FR-PLAT-TAX-002, FR-ADM-TAX-001). Each row carries its owning subject's name.
/// </summary>
public sealed record ListSpecializationsQuery(Guid? SubjectId = null)
    : IRequest<IReadOnlyList<SpecializationDto>>;
