using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudentActivity;

/// <summary>Paged activity history for a student — every audit entry keyed to them (FR-ADM-STU-008).</summary>
public sealed record ListStudentActivityQuery(
    Guid StudentId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<StudentAuditEntryDto>>;
