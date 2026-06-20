namespace SalahBahazad.Application.Features.Audit;

/// <summary>
/// The "who-read-what" subset of audit actions hidden from callers without <c>AuditReadSensitive</c>
/// (contract §4, FR-ADM-AUD-003 — the prototype's "Scoped view" alert). Teachers see all; Assistants see the
/// rest. 5A surfaces no new sensitive <i>read</i> screen, so it introduces no <c>AuditViewed</c> action — the
/// single sensitive action today is the Phase-2 student ID-image view. Append future sensitive-read actions here.
/// </summary>
public static class SensitiveAuditActions
{
    /// <summary>An array so it inlines cleanly into the SQL <c>NOT IN (...)</c> filter via <c>EF.Constant</c>.</summary>
    public static readonly string[] All = ["StudentIdImageViewed"];

    private static readonly HashSet<string> Set = new(All, StringComparer.Ordinal);

    public static bool Contains(string? action) => action is not null && Set.Contains(action);
}
