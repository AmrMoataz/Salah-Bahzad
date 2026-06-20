using Mediator;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetQuizReview;

/// <summary>
/// The "Quiz attempts" review of a student's gating quiz for an enrollment (contract §B #6, FR-ADM-REV-002 —
/// scrReview). Staff-only; tenant-scoped by the global filter. 404 when the enrollment has no quiz.
/// </summary>
public sealed record GetQuizReviewQuery(Guid EnrollmentId) : IRequest<QuizReviewDto>;
