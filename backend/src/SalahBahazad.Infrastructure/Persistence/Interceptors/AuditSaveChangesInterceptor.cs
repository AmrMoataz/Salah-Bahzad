using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Intercepts SaveChanges to stamp audit fields and write hash-chained AuditEntry rows
/// atomically with the action (NFR-AUD-001/003, FR-PLAT-AUD-001). Each entry's <c>Hash</c>
/// covers its content plus the previous entry's hash, so deletions or back-dating break the chain.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ICurrentUserResolver currentUser,
    ICurrentTenantResolver tenantResolver,
    IAuditContextAccessor auditContext,
    TimeProvider clock)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            StampAndAudit(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            StampAndAudit(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampAndAudit(DbContext context)
    {
        var now = clock.GetUtcNow();
        var isAuthenticated = currentUser.IsAuthenticated;
        var actorId = isAuthenticated ? currentUser.UserId : (Guid?)null;
        var actorType = currentUser.ActorType; // "Staff" | "Student" | "System"
        var actorRole = actorType switch
        {
            "Staff" => currentUser.Role.ToString(),
            "Student" => "Student",
            _ => null,
        };
        var deviceId = currentUser.DeviceId;
        var ipAddress = auditContext.IpAddress;
        var portal = auditContext.Portal;
        var tenantId = tenantResolver.IsResolved ? tenantResolver.TenantId : Guid.Empty;

        foreach (var entry in context.ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedById = actorId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedById = actorId;
                    break;
            }
        }

        // Without a resolved tenant (e.g. the pre-auth login save) there is nothing to scope an
        // audit entry to — skip auditing but keep the field stamps above.
        if (tenantId == Guid.Empty)
            return;

        var auditable = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditEntry)
            .ToList();

        if (auditable.Count == 0)
            return;

        // Seed the chain from the tenant's most recent committed entry (Id is UUIDv7 = time-ordered).
        var prevHash = context.Set<AuditEntry>()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefault();

        foreach (var entry in auditable)
        {
            // Entities audited solely via their aggregate's semantic events (bulk-minted codes, per-video
            // access counters, payment + attendance shells) must not emit a generic field-diff row unless
            // they carry a semantic event this save — keeps one entry per lifecycle event (FR-PLAT-AUD-002).
            if (entry.Entity is IAuditViaEventOnly)
            {
                var carriesSemanticEvent = entry.Entity is EntityBase eventfulEntity
                    && eventfulEntity.DomainEvents.OfType<IAuditableDomainEvent>().Any();
                if (!carriesSemanticEvent)
                    continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => "Unknown",
            };

            var entityType = entry.Entity.GetType().Name;

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "Id")
                ?.CurrentValue as Guid?;

            var beforeJson = entry.State == EntityState.Added
                ? null
                : Serialize(entry.OriginalValues.ToObject());

            var afterJson = entry.State == EntityState.Deleted
                ? null
                : Serialize(entry.CurrentValues.ToObject());

            var summary = $"{action} {entityType}";

            // Semantic enrichment: prefer a buffered IAuditableDomainEvent's action/summary so this
            // single chained row reads meaningfully (e.g. "StudentRejected: duplicate account"). The
            // field-diff above still records the underlying change; events are still buffered here
            // because dispatch happens post-commit (FR-PLAT-AUD-002).
            if (entry.Entity is EntityBase entity)
            {
                var semantic = entity.DomainEvents.OfType<IAuditableDomainEvent>().FirstOrDefault();
                if (semantic is not null)
                {
                    action = semantic.AuditAction;
                    summary = semantic.AuditSummary;
                }
            }

            var auditEntry = AuditEntry.Create(
                tenantId: tenantId,
                action: action,
                entityType: entityType,
                occurredAtUtc: now,
                entityId: entityId,
                actorId: actorId,
                actorRole: actorRole,
                actorType: actorType,
                summary: summary,
                beforeJson: beforeJson,
                afterJson: afterJson,
                ipAddress: ipAddress,
                portal: portal,
                deviceId: deviceId,
                prevHash: prevHash);

            var hash = AuditHasher.ComputeHash(prevHash, auditEntry);
            auditEntry.SetHash(hash);
            context.Set<AuditEntry>().Add(auditEntry);

            // Chain forward so multiple entries in one save are linked in order.
            prevHash = hash;
        }
    }

    private static string? Serialize(object? obj)
    {
        if (obj is null) return null;
        try
        {
            return JsonSerializer.Serialize(obj);
        }
        catch
        {
            return null;
        }
    }
}
