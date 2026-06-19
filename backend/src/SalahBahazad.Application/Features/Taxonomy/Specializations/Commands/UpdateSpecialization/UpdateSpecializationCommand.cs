using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.UpdateSpecialization;

/// <summary>
/// Renames a specialization and/or reassigns it to another subject (FR-ADM-TAX-001).
/// The target subject must exist in the caller's tenant.
/// </summary>
public sealed record UpdateSpecializationCommand(Guid Id, Guid SubjectId, string Name)
    : IRequest<SpecializationDto>, ITransactionalRequest;
