using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Questions.Commands.RemoveQuestionVariation;

/// <summary>Removes a variation from a question (FR-ADM-QB-006).</summary>
public sealed record RemoveQuestionVariationCommand(Guid SessionId, Guid QuestionId, Guid VariationId)
    : IRequest<Unit>, ITransactionalRequest;
