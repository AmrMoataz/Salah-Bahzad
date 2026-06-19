using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.CreateQuestion;

/// <summary>
/// Adds an MCQ question to a session's bank (FR-PLAT-QB-001, FR-ADM-QB-001). An image may be supplied
/// inline (base64) so an image-only question can be created atomically (FR-PLAT-QB-002); otherwise a
/// LaTeX body is required. The image can still be replaced afterwards via the multipart image endpoint (#22).
/// </summary>
public sealed record CreateQuestionCommand(
    Guid SessionId,
    string? BodyLatex,
    int Mark,
    bool IsValidForQuiz,
    string? HintUrl,
    IReadOnlyList<QuestionOptionInput> Options,
    string? ImageBase64 = null,
    string? ImageContentType = null) : IRequest<QuestionDto>, ITransactionalRequest;
