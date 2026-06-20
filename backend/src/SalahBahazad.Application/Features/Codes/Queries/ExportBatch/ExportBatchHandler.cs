using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Queries.ExportBatch;

internal sealed class ExportBatchHandler(IAppDbContext db, ICodeExporter exporter, IAuditWriter auditWriter)
    : IRequestHandler<ExportBatchQuery, CodeCsvFile>
{
    public async ValueTask<CodeCsvFile> Handle(ExportBatchQuery query, CancellationToken cancellationToken)
    {
        var batch = await db.CodeBatches.FirstOrDefaultAsync(b => b.Id == query.BatchId, cancellationToken)
            ?? throw new NotFoundException("CodeBatch", query.BatchId);

        var codes = await db.Codes
            .AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = await CodeListProjector.ToListDtosAsync(db, codes, cancellationToken);
        var csv = exporter.BuildCsv([.. dtos.Select(d => d.ToExportRow())]);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "CodesExported",
                EntityType: "CodeBatch",
                EntityId: batch.Id,
                Summary: $"Exported batch {batch.Label} ({codes.Count} code(s)) to CSV."),
            cancellationToken);

        return new CodeCsvFile(csv, $"{batch.Label}.csv");
    }
}
