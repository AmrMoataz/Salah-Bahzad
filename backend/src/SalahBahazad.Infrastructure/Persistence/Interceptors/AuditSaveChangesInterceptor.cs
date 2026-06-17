using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Intercepts SaveChanges to stamp audit fields and write AuditEntry rows
/// atomically with the action (NFR-AUD-003, FR-PLAT-AUD-001).
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ICurrentUserResolver currentUser,
    ICurrentTenantResolver tenantResolver)
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
        var now = DateTimeOffset.UtcNow;
        var actorId = currentUser.IsAuthenticated ? currentUser.UserId : (Guid?)null;
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

        // Append audit entries for tracked changes.
        var auditable = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditEntry)
            .ToList();

        foreach (var entry in auditable)
        {
            if (tenantId == Guid.Empty) continue;

            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => "Unknown",
            };

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "Id")
                ?.CurrentValue as Guid?;

            var beforeJson = entry.State == EntityState.Added
                ? null
                : Serialize(entry.OriginalValues.ToObject());

            var afterJson = entry.State == EntityState.Deleted
                ? null
                : Serialize(entry.CurrentValues.ToObject());

            var auditEntry = AuditEntry.Create(
                tenantId: tenantId,
                action: action,
                entityType: entry.Entity.GetType().Name,
                entityId: entityId,
                actorId: actorId,
                actorType: actorId.HasValue ? "Staff" : "System",
                beforeJson: beforeJson,
                afterJson: afterJson);

            context.Set<AuditEntry>().Add(auditEntry);
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
