using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Codes.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Codes.Queries.ListCodes;

/// <summary>
/// The code register (FR-PLAT-COD-005, scrCodes): filter by free-text (serial or redeemed-by student),
/// status, batch and session, paged, with batch/session/student/creator names resolved per row.
/// </summary>
public sealed record ListCodesQuery(
    string? Search = null,
    CodeStatus? Status = null,
    Guid? BatchId = null,
    Guid? SessionId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<CodeListDto>>;
