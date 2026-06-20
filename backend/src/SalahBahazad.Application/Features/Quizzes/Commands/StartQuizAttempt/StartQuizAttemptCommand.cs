using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Quizzes.DTOs;

namespace SalahBahazad.Application.Features.Quizzes.Commands.StartQuizAttempt;

/// <summary>
/// Starts a new attempt at the caller's quiz (contract §A #2, FR-PLAT-QZ-003): draws a fresh randomised subset
/// of the source bank, sets the authoritative deadline, and consumes an attempt. 409 if attempts are exhausted
/// or one is already active. Transactional so the attempt + its started-event commit together; the started event
/// then schedules the auto-submit timer post-commit (FR-PLAT-QZ-005).
/// </summary>
public sealed record StartQuizAttemptCommand(Guid QuizId) : IRequest<QuizAttemptDto>, ITransactionalRequest;
