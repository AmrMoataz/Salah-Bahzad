using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Questions.Commands.DeleteQuestion;

/// <summary>Soft-deletes ("detaches") a question so generated assignments/quizzes and history survive
/// (FR-ADM-QB-006, FR-PLAT-ROLE-004).</summary>
public sealed record DeleteQuestionCommand(Guid SessionId, Guid QuestionId)
    : IRequest<Unit>, ITransactionalRequest;
