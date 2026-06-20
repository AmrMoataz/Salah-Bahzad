using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Audit.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Audit;

/// <summary>
/// Resolves the display joins for a page of <see cref="AuditEntry"/> rows — the bold <b>actor</b> name, the bold
/// <b>target</b> label, and the derived <c>category</c> — and projects them to <see cref="AuditFeedItem"/>.
/// Shared verbatim by the activity feed (<c>ListAudit</c>) and the dashboard's recent-activity (contract §1/§3).
/// <para>
/// Lookups use <c>IgnoreQueryFilters</c> so a soft-deleted/archived entity still shows its name; this is safe
/// because every id comes from a row already scoped to the caller's tenant (the handlers add the explicit
/// <c>TenantId</c> filter — <see cref="AuditEntry"/> is NOT tenant-filtered globally, NFR-SEC-010). Mirrors
/// <c>CodeListProjector</c>: one batched query per entity type, no per-row round-trips.
/// </para>
/// </summary>
internal static class AuditFeedProjector
{
    public static async Task<List<AuditFeedItem>> ToFeedItemsAsync(
        IAppDbContext db, IReadOnlyList<AuditEntry> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        // ── Actor ids (staff vs student), keyed off ActorType ──────────────────
        var staffActorIds = rows
            .Where(a => a.ActorType == "Staff" && a.ActorId.HasValue)
            .Select(a => a.ActorId!.Value);

        var studentActorIds = rows
            .Where(a => a.ActorType == "Student" && a.ActorId.HasValue)
            .Select(a => a.ActorId!.Value);

        // ── Target ids grouped by EntityType (the affected entity) ─────────────
        var targetIdsByType = rows
            .Where(a => a.EntityId.HasValue)
            .GroupBy(a => a.EntityType, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(a => a.EntityId!.Value).Distinct().ToList(), StringComparer.Ordinal);

        IReadOnlyList<Guid> TargetIds(string entityType) =>
            targetIdsByType.TryGetValue(entityType, out var ids) ? ids : [];

        var studentTargetIds = TargetIds("Student");
        var sessionIds = TargetIds("Session");
        var codeIds = TargetIds("Code");
        var staffTargetIds = TargetIds("Staff");
        var enrollmentIds = TargetIds("Enrollment");

        // Enrollment rows show the affected student as the target (matches the prototype seed + View→student).
        var enrollmentStudent = enrollmentIds.Count == 0
            ? []
            : await db.Enrollments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(e => enrollmentIds.Contains(e.Id))
                .Select(e => new { e.Id, e.StudentId })
                .ToDictionaryAsync(e => e.Id, e => e.StudentId, cancellationToken);

        // ── Batched name lookups (one query per entity type) ───────────────────
        var allStudentIds = studentActorIds
            .Concat(studentTargetIds)
            .Concat(enrollmentStudent.Values)
            .Distinct()
            .ToList();
        var studentNames = allStudentIds.Count == 0
            ? []
            : await db.Students
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => allStudentIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var allStaffIds = staffActorIds.Concat(staffTargetIds).Distinct().ToList();
        var staffNames = allStaffIds.Count == 0
            ? []
            : await db.Staff
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => allStaffIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.DisplayName, cancellationToken);

        var sessionTitles = sessionIds.Count == 0
            ? []
            : await db.Sessions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => sessionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Title, cancellationToken);

        var codeSerials = codeIds.Count == 0
            ? []
            : await db.Codes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => codeIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Serial, cancellationToken);

        return [.. rows.Select(a => a.ToFeedItem(
            ResolveActorName(a, staffNames, studentNames),
            // Narrow the role badge to staff roles (contract: "Teacher | Assistant | null").
            a.ActorType == "Staff" ? a.ActorRole : null,
            ResolveTargetLabel(a, studentNames, staffNames, sessionTitles, codeSerials, enrollmentStudent),
            AuditActionCategory.CategoryOf(a.Action)))];
    }

    private static string? ResolveActorName(
        AuditEntry a,
        IReadOnlyDictionary<Guid, string> staffNames,
        IReadOnlyDictionary<Guid, string> studentNames) => a.ActorType switch
        {
            "System" => "System",
            "Staff" => a.ActorId is Guid id ? staffNames.GetValueOrDefault(id) : null,
            "Student" => a.ActorId is Guid id ? studentNames.GetValueOrDefault(id) : null,
            _ => null,
        };

    private static string? ResolveTargetLabel(
        AuditEntry a,
        IReadOnlyDictionary<Guid, string> studentNames,
        IReadOnlyDictionary<Guid, string> staffNames,
        IReadOnlyDictionary<Guid, string> sessionTitles,
        IReadOnlyDictionary<Guid, string> codeSerials,
        IReadOnlyDictionary<Guid, Guid> enrollmentStudent)
    {
        if (a.EntityId is not Guid id)
            return null;

        return a.EntityType switch
        {
            "Student" => studentNames.GetValueOrDefault(id),
            "Session" => sessionTitles.GetValueOrDefault(id),
            "Code" => codeSerials.GetValueOrDefault(id),
            "Staff" => staffNames.GetValueOrDefault(id),
            "Enrollment" => enrollmentStudent.TryGetValue(id, out var studentId)
                ? studentNames.GetValueOrDefault(studentId)
                : null,
            _ => null,
        };
    }
}
