using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudentLoginHistory;

/// <summary>Paged sign-in history for a student (FR-ADM-STU-008). Sourced from sign-in audit entries;
/// empty until student-portal authentication (Phase 3) begins recording them.</summary>
public sealed record ListStudentLoginHistoryQuery(
    Guid StudentId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<StudentAuditEntryDto>>;
