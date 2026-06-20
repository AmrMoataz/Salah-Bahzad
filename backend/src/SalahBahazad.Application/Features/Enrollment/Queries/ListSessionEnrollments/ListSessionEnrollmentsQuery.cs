using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment.Queries.ListSessionEnrollments;

/// <summary>The "Enrolled students" tab of a session (#8): rows of who is enrolled, with optional name search.</summary>
public sealed record ListSessionEnrollmentsQuery(
    Guid SessionId, string? Search = null, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<EnrollmentListDto>>;
