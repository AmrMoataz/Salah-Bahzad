using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.AddQuestionVariation;

/// <summary>Adds a variation (alternate wording + own options) to a question (FR-PLAT-QB-003,
/// FR-ADM-QB-004). An image may be supplied inline (base64) so an image-only variation can be created
/// atomically (FR-PLAT-QB-002); otherwise a LaTeX body is required. It can still be replaced later via #26.</summary>
public sealed record AddQuestionVariationCommand(
    Guid SessionId,
    Guid QuestionId,
    string? BodyLatex,
    IReadOnlyList<QuestionOptionInput> Options,
    string? ImageBase64 = null,
    string? ImageContentType = null) : IRequest<QuestionVariationDto>, ITransactionalRequest;
