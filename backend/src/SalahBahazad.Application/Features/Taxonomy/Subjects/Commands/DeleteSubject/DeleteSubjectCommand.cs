using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.DeleteSubject;

/// <summary>
/// Soft-deletes a subject (FR-PLAT-TAX-004, FR-PLAT-ROLE-004). A subject that still has live
/// specializations is "in use" and cannot be deleted — the handler raises a conflict instead.
/// </summary>
public sealed record DeleteSubjectCommand(Guid Id) : IRequest<Unit>, ITransactionalRequest;
