using Mediator;

namespace SalahBahazad.Application.Features.Quizzes.Commands.AnswerQuizQuestion;

/// <summary>
/// Records the calling student's answer to one drawn question of an active attempt (contract §A #3). 409 if the
/// attempt is not in progress or its deadline has passed; 404 for an unknown question/option. A single atomic
/// save — no domain event — so it is not transactional.
/// </summary>
public sealed record AnswerQuizQuestionCommand(Guid AttemptId, Guid AttemptQuestionId, Guid SelectedOptionId)
    : IRequest<Unit>;
