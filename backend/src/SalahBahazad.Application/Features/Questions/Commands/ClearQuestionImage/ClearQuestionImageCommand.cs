using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.ClearQuestionImage;

/// <summary>Removes the question image (DELETE on the image route). Illegal when it would leave the
/// question with no content (FR-PLAT-QB-002) → 409.</summary>
public sealed record ClearQuestionImageCommand(Guid SessionId, Guid QuestionId)
    : IRequest<QuestionDto>, ITransactionalRequest;
