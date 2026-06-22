using Mediator;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListCatalogue;

/// <summary>
/// The student-portal catalogue read (S2, contract §A, FR-STU-CAT-001/002/004): the caller's tenant's
/// <b>published</b> sessions as a flat, newest-first list, each carrying display fields, a prerequisite
/// badge + satisfied flag, and the caller's own enrollment state. All filters are optional and narrowing;
/// subject is matched via the session's specialization (FR-PLAT-TAX-002). The student + tenant come from the
/// JWT (<see cref="Common.Interfaces.ICurrentUserResolver"/>), never a URL id — no IDOR surface. Not paginated.
/// </summary>
public sealed record ListCatalogueQuery(
    string? Search = null,
    Guid? SubjectId = null,
    Guid? SpecializationId = null) : IRequest<IReadOnlyList<CatalogueSessionDto>>;
