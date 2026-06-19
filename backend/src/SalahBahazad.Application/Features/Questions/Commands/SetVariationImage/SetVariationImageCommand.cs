using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.SetVariationImage;

/// <summary>Uploads/replaces a variation's image (FR-PLAT-QB-002/003).</summary>
public sealed record SetVariationImageCommand(
    Guid SessionId,
    Guid QuestionId,
    Guid VariationId,
    Stream Content,
    string ContentType,
    long Length,
    string FileName) : IRequest<QuestionVariationDto>, ITransactionalRequest;
