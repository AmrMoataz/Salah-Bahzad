using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.CreateSpecialization;

/// <summary>
/// Creates a specialization under an existing subject (FR-PLAT-TAX-002, FR-ADM-TAX-001).
/// The owning subject must exist in the caller's tenant.
/// </summary>
public sealed record CreateSpecializationCommand(Guid SubjectId, string Name)
    : IRequest<SpecializationDto>, ITransactionalRequest;
