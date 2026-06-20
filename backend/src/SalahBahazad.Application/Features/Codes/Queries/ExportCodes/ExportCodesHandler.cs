using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Queries.ExportCodes;

internal sealed class ExportCodesHandler(
    IAppDbContext db, ICodeExporter exporter, IAuditWriter auditWriter, TimeProvider clock)
    : IRequestHandler<ExportCodesQuery, CodeCsvFile>
{
    public async ValueTask<CodeCsvFile> Handle(ExportCodesQuery query, CancellationToken cancellationToken)
    {
        var codes = await CodeFilters
            .Apply(db.Codes.AsNoTracking(), db, query.Search, query.Status, query.BatchId, query.SessionId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = await CodeListProjector.ToListDtosAsync(db, codes, cancellationToken);
        var csv = exporter.BuildCsv([.. dtos.Select(d => d.ToExportRow())]);

        // A GET never reaches SaveChanges, so the interceptor cannot capture it — audit explicitly (FR-PLAT-AUD-002).
        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "CodesExported",
                EntityType: "Code",
                EntityId: Guid.Empty,
                Summary: $"Exported {codes.Count} code(s) to CSV."),
            cancellationToken);

        return new CodeCsvFile(csv, $"codes-{clock.GetUtcNow():yyyyMMdd-HHmmss}.csv");
    }
}
