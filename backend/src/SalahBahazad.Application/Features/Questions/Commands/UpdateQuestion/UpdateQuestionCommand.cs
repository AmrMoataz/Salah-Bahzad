using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.UpdateQuestion;

/// <summary>Edits a question's body, mark, quiz-eligibility, hint, and options (FR-ADM-QB-006). The body
/// may be cleared only if an image is already attached (FR-PLAT-QB-002, enforced in the handler).</summary>
public sealed record UpdateQuestionCommand(
    Guid SessionId,
    Guid QuestionId,
    string? BodyLatex,
    int Mark,
    bool IsValidForQuiz,
    string? HintUrl,
    IReadOnlyList<QuestionOptionInput> Options) : IRequest<QuestionDto>, ITransactionalRequest;
