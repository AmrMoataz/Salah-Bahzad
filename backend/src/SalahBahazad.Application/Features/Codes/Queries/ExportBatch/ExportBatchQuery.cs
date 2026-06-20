using Mediator;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Queries.ExportBatch;

/// <summary>
/// Re-exports a single batch as a CSV (#4, FR-ADM-COD-005) — the Generate screen's "Download Excel" button.
/// Audited server-side like #3.
/// </summary>
public sealed record ExportBatchQuery(Guid BatchId) : IRequest<CodeCsvFile>;
