namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Serializes a filtered set of codes to a CSV file (FR-PLAT-COD-002, FR-ADM-COD-005). Synchronous and
/// in-memory — fine for batch sizes ≤ 1000; a Hangfire async export and a true <c>.xlsx</c> are a later
/// drop-in upgrade. The column set is the contract §2 set:
/// <c>Serial, Value, Status, Batch, Session, Created by, Created, Redeemed by, Redeemed at</c>.
/// </summary>
public interface ICodeExporter
{
    byte[] BuildCsv(IReadOnlyList<CodeExportRow> rows);
}

/// <summary>One CSV line of code data (decoupled from the feature DTO so this lives in Common).</summary>
public sealed record CodeExportRow(
    string Serial,
    decimal Value,
    string Status,
    string? BatchLabel,
    string? SessionTitle,
    string? CreatedByName,
    DateTimeOffset CreatedAtUtc,
    string? RedeemedByName,
    DateTimeOffset? RedeemedAtUtc);
