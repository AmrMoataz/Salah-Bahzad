using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.CreateGrade;

/// <summary>Creates a tenant-scoped grade level (FR-PLAT-TAX-001, FR-ADM-TAX-001).</summary>
public sealed record CreateGradeCommand(string Name) : IRequest<GradeDto>, ITransactionalRequest;
