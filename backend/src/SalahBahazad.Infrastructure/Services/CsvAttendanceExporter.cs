using System.Globalization;
using System.Text;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Streams attendance rows to RFC 4180 CSV (<see cref="IAttendanceExporter"/>). Writes a UTF-8 BOM so Excel
/// renders Arabic student/session names correctly, and quotes any field containing a comma, quote or newline.
/// The leading column is caller-supplied (Student or Session); the rest are the contract §B matrix metrics, with
/// the pending (5C/5B-2) columns rendered as the literal "—" placeholder. Mirrors <c>CsvCodeExporter</c>.
/// </summary>
internal sealed class CsvAttendanceExporter : IAttendanceExporter
{
    public byte[] BuildCsv(string keyColumnHeader, IReadOnlyList<AttendanceExportRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyColumnHeader);
        ArgumentNullException.ThrowIfNull(rows);

        string[] header =
            [keyColumnHeader, "Videos watched", "Videos total", "Assignment %", "Best quiz %", "Quiz attempts"];

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', header.Select(Escape)));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',',
                Escape(row.KeyLabel),
                Escape(Int(row.VideosWatched)),
                Escape(Int(row.VideosTotal)),
                Escape(Percent(row.AssignmentPercent)),
                Escape(Percent(row.BestQuizPercent)),
                Escape(Int(row.QuizAttemptCount))));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Percent(int? value)
        => value is int v ? v.ToString(CultureInfo.InvariantCulture) : "—";

    private static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";

        return field;
    }
}
