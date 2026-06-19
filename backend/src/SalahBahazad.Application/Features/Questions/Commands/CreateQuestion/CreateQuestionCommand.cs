using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.CreateQuestion;

/// <summary>
/// Adds an MCQ question to a session's bank (FR-PLAT-QB-001, FR-ADM-QB-001). The image is uploaded
/// separately (#22), so a LaTeX body is required here.
/// </summary>
public sealed record CreateQuestionCommand(
    Guid SessionId,
    string? BodyLatex,
    int Mark,
    bool IsValidForQuiz,
    string? HintUrl,
    IReadOnlyList<QuestionOptionInput> Options) : IRequest<QuestionDto>, ITransactionalRequest;
