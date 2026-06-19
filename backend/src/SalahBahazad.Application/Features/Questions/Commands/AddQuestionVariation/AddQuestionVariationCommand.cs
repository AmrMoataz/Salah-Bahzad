using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.AddQuestionVariation;

/// <summary>Adds a variation (alternate wording + own options) to a question (FR-PLAT-QB-003,
/// FR-ADM-QB-004). The image is uploaded separately (#26), so a LaTeX body is required here.</summary>
public sealed record AddQuestionVariationCommand(
    Guid SessionId,
    Guid QuestionId,
    string? BodyLatex,
    IReadOnlyList<QuestionOptionInput> Options) : IRequest<QuestionVariationDto>, ITransactionalRequest;
