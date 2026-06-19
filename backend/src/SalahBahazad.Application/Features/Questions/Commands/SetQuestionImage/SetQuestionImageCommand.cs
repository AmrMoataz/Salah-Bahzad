using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.SetQuestionImage;

/// <summary>Uploads/replaces the question image (FR-PLAT-QB-002, FR-ADM-QB-003).</summary>
public sealed record SetQuestionImageCommand(
    Guid SessionId,
    Guid QuestionId,
    Stream Content,
    string ContentType,
    long Length,
    string FileName) : IRequest<QuestionDto>, ITransactionalRequest;
