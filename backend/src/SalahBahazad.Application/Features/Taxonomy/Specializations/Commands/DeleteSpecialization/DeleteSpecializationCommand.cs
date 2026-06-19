using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.DeleteSpecialization;

/// <summary>
/// Soft-deletes a specialization (FR-PLAT-TAX-004, FR-PLAT-ROLE-004). No Session references exist
/// yet, so a specialization is always free to soft-delete today.
/// </summary>
public sealed record DeleteSpecializationCommand(Guid Id) : IRequest<Unit>, ITransactionalRequest;
