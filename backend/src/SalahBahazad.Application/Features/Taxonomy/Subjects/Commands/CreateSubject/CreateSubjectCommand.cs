using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.CreateSubject;

/// <summary>Creates a tenant-scoped subject (FR-PLAT-TAX-001/002, FR-ADM-TAX-001).</summary>
public sealed record CreateSubjectCommand(string Name) : IRequest<SubjectDto>, ITransactionalRequest;
