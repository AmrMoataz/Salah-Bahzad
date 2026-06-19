using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.UpdateSubject;

/// <summary>Renames a subject (FR-ADM-TAX-001).</summary>
public sealed record UpdateSubjectCommand(Guid Id, string Name) : IRequest<SubjectDto>, ITransactionalRequest;
