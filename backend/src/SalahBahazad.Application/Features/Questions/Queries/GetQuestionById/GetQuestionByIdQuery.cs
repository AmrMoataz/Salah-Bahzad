using Mediator;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Queries.GetQuestionById;

/// <summary>Full detail for a single bank question (scrQuestionEditor load).</summary>
public sealed record GetQuestionByIdQuery(Guid SessionId, Guid QuestionId) : IRequest<QuestionDto>;
