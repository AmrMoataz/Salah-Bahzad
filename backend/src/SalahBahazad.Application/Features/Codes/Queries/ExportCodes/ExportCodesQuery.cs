using Mediator;
using SalahBahazad.Application.Features.Codes.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Codes.Queries.ExportCodes;

/// <summary>
/// Exports the whole filtered code set as a CSV (#3, FR-PLAT-COD-002) — same filters as the register (#1).
/// The export is audited server-side (it is a GET the interceptor cannot capture; contract §5).
/// </summary>
public sealed record ExportCodesQuery(
    string? Search = null,
    CodeStatus? Status = null,
    Guid? BatchId = null,
    Guid? SessionId = null) : IRequest<CodeCsvFile>;
