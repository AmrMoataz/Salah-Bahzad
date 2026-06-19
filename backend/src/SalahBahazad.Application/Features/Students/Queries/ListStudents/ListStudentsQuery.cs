using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Students.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudents;

/// <summary>Lists students filtered by status and grade, with free-text search, paginated (FR-ADM-STU-001).</summary>
public sealed record ListStudentsQuery(
    string? Search = null,
    StudentStatus? Status = null,
    Guid? GradeId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<StudentListDto>>;
