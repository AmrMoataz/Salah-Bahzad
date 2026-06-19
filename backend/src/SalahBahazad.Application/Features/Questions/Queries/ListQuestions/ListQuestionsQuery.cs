using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Queries.ListQuestions;

/// <summary>Lists a session's question bank, paginated, with signed image URLs embedded (FR-ADM-QB-001).</summary>
public sealed record ListQuestionsQuery(
    Guid SessionId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<QuestionDto>>;
