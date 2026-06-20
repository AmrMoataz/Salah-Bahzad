using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Quizzes.DTOs;

namespace SalahBahazad.Application.Features.Quizzes.Commands.SubmitQuizAttempt;

/// <summary>
/// Submits the calling student's active attempt (contract §A #4, FR-PLAT-QZ-007): grades it, updates best-of +
/// pass (≥, FR-PLAT-QZ-008), and consumes the attempt. 409 if the attempt is already terminal. Transactional so
/// the grade commits before the submitted event cancels the timer and the grade event writes attendance.
/// </summary>
public sealed record SubmitQuizAttemptCommand(Guid AttemptId) : IRequest<QuizAttemptResultDto>, ITransactionalRequest;
