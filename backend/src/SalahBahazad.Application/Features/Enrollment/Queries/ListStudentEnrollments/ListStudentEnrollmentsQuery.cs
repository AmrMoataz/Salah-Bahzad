using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment.Queries.ListStudentEnrollments;

/// <summary>The "Enrollments &amp; transactions" tab of a student (#11): the student's session enrollments.</summary>
public sealed record ListStudentEnrollmentsQuery(Guid StudentId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<StudentEnrollmentDto>>;
