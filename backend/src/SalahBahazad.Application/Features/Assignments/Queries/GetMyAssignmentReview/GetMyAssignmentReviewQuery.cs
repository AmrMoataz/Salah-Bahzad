using Mediator;
using SalahBahazad.Application.Features.Assignments.DTOs;

namespace SalahBahazad.Application.Features.Assignments.Queries.GetMyAssignmentReview;

/// <summary>
/// The calling student's <b>answer-key review</b> of their own assignment (contract §B, FR-STU-ASG-007,
/// FR-PLAT-ASG-008) — the only student surface that exposes option correctness, and only post-completion. The
/// student is read from the JWT; the assignment is resolved by id AND ownership (IDOR/cross-tenant → 404), and
/// gated to <c>Completed</c> (an in-progress assignment → 403 <c>assignment_in_progress</c>).
/// </summary>
public sealed record GetMyAssignmentReviewQuery(Guid AssignmentId) : IRequest<StudentAssignmentReviewDto>;
