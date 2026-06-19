using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.DeleteGrade;

/// <summary>
/// Soft-deletes a grade (FR-PLAT-TAX-004, FR-PLAT-ROLE-004). Soft-delete preserves historical
/// references; the name is freed for reuse because the unique index is filtered to live rows.
/// </summary>
public sealed record DeleteGradeCommand(Guid Id) : IRequest<Unit>, ITransactionalRequest;
