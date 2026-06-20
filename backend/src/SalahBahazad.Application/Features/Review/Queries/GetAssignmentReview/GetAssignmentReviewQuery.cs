using Mediator;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetAssignmentReview;

/// <summary>
/// The staff per-question review of a student's assignment for an enrollment (contract §C #7, FR-ADM-REV-001):
/// submitted-vs-correct, score and time. Staff-only — shows correctness. 404 when the enrollment has no
/// assignment (or is not the caller's tenant).
/// </summary>
public sealed record GetAssignmentReviewQuery(Guid EnrollmentId) : IRequest<AssignmentReviewDto>;
