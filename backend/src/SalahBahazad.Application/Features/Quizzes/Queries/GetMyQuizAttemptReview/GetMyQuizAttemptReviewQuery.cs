using Mediator;
using SalahBahazad.Application.Features.Quizzes.DTOs;

namespace SalahBahazad.Application.Features.Quizzes.Queries.GetMyQuizAttemptReview;

/// <summary>
/// The calling student's <b>answer-key review</b> of their own quiz attempt (contract §B, FR-STU-QZ-009) — the
/// only student surface that exposes quiz option correctness, and only post-termination. The student is read from
/// the JWT; the attempt is resolved <b>through its owning <c>UserQuiz</c></b> by id AND ownership (an unknown /
/// another student's / another tenant's id → 404), and gated to a <b>terminal</b> attempt (an <c>InProgress</c>
/// attempt → 403 <c>quiz_attempt_in_progress</c> — the key is never revealed mid-sitting).
/// </summary>
public sealed record GetMyQuizAttemptReviewQuery(Guid AttemptId) : IRequest<StudentQuizAttemptReviewDto>;
