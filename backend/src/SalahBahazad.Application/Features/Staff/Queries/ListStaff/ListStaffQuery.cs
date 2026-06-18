using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Staff.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Staff.Queries.ListStaff;

/// <summary>Lists staff filtered by role/status and a free-text search, paginated (FR-ADM-STAFF-001).</summary>
public sealed record ListStaffQuery(
    string? Search = null,
    StaffRole? Role = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<StaffDto>>;
