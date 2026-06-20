using System.Globalization;
using System.Text;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Streams codes to RFC 4180 CSV (<see cref="ICodeExporter"/>). Writes a UTF-8 BOM so Excel renders Arabic
/// student/session names correctly, and quotes any field containing a comma, quote, or newline. The column
/// order matches the contract §2 set and the prototype's <c>downloadCSV</c>.
/// </summary>
internal sealed class CsvCodeExporter : ICodeExporter
{
    private static readonly string[] Header =
        ["Serial", "Value", "Status", "Batch", "Session", "Created by", "Created", "Redeemed by", "Redeemed at"];

    public byte[] BuildCsv(IReadOnlyList<CodeExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Header.Select(Escape)));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',',
                Escape(row.Serial),
                Escape(row.Value.ToString(CultureInfo.InvariantCulture)),
                Escape(row.Status),
                Escape(row.BatchLabel),
                Escape(row.SessionTitle),
                Escape(row.CreatedByName),
                Escape(FormatDate(row.CreatedAtUtc)),
                Escape(row.RedeemedByName),
                Escape(row.RedeemedAtUtc is null ? null : FormatDate(row.RedeemedAtUtc.Value))));
        }

        // UTF-8 BOM + content so Excel detects the encoding.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string FormatDate(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";

        return field;
    }
}
