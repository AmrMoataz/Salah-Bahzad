using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Audit;

/// <summary>
/// The shared audit-feed filter (contract #1), applied identically wherever the feed is read so paging and the
/// total count always agree. Every predicate runs in SQL — including the <c>category → action-set</c> and the
/// sensitive-exclusion (<c>NOT IN</c>) — so <c>Skip</c>/<c>Take</c> page over the correctly-filtered set.
/// <para>
/// This does NOT add the tenant predicate: <see cref="AuditEntry"/> is not globally tenant-filtered, so the
/// caller (handler) must apply <c>Where(a =&gt; a.TenantId == tenant.TenantId)</c> first (NFR-SEC-010). The
/// caller also resolves <c>period → from/to</c> before calling this.
/// </para>
/// </summary>
internal static class AuditFilters
{
    public static IQueryable<AuditEntry> Apply(
        IQueryable<AuditEntry> query,
        Guid? actorId,
        string? actorType,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? studentId,
        Guid? sessionId,
        string? entityType,
        Guid? entityId,
        bool includeSensitive)
    {
        if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);

        if (!string.IsNullOrWhiteSpace(actorType))
            query = query.Where(a => a.ActorType == actorType);

        if (!string.IsNullOrWhiteSpace(category))
        {
            if (string.Equals(category, AuditActionCategory.Other, StringComparison.OrdinalIgnoreCase))
            {
                // "Other" is the complement of every mapped action (generic Created/Updated/Deleted rows, …).
                var mapped = AuditActionCategory.AllMappedActions;
                query = query.Where(a => !EF.Constant(mapped).Contains(a.Action));
            }
            else
            {
                // Inline the action set as constants so the IN-list participates in the (TenantId, Action) index.
                var actions = AuditActionCategory.ActionsInCategory(category).ToArray();
                query = query.Where(a => EF.Constant(actions).Contains(a.Action));
            }
        }

        if (from.HasValue)
            query = query.Where(a => a.OccurredAtUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.OccurredAtUtc <= to.Value);

        // Entity-scoped tabs (student-detail / session-detail / generic) match on the affected entity.
        if (studentId.HasValue)
            query = query.Where(a => a.EntityId == studentId.Value);

        if (sessionId.HasValue)
            query = query.Where(a => a.EntityId == sessionId.Value);

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId.Value);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        // Assistant scope (contract §4): hide the "who-read-what" actions unless the caller may read sensitive.
        if (!includeSensitive)
        {
            var sensitive = SensitiveAuditActions.All;
            query = query.Where(a => !EF.Constant(sensitive).Contains(a.Action));
        }

        return query;
    }
}
