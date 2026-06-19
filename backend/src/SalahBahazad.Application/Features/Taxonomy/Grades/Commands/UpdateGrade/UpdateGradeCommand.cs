using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.UpdateGrade;

/// <summary>Renames a grade level (FR-ADM-TAX-001).</summary>
public sealed record UpdateGradeCommand(Guid Id, string Name) : IRequest<GradeDto>, ITransactionalRequest;
