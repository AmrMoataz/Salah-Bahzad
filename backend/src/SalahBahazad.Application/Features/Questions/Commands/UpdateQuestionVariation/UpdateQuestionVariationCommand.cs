using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.UpdateQuestionVariation;

/// <summary>Edits a variation's body and options (FR-ADM-QB-004/006). The body may be cleared only if the
/// variation already has an image (FR-PLAT-QB-002, enforced in the handler).</summary>
public sealed record UpdateQuestionVariationCommand(
    Guid SessionId,
    Guid QuestionId,
    Guid VariationId,
    string? BodyLatex,
    IReadOnlyList<QuestionOptionInput> Options) : IRequest<QuestionVariationDto>, ITransactionalRequest;
