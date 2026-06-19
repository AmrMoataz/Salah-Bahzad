using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Infrastructure.Persistence;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Writes explicit, hash-chained <see cref="AuditEntry"/> rows (see <see cref="IAuditWriter"/>),
/// seeding the chain from the tenant's most recent committed entry and using the shared
/// <see cref="AuditHasher"/> so these rows interleave correctly with interceptor-written ones.
/// </summary>
internal sealed class AuditWriter(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    ICurrentTenantResolver tenantResolver,
    IAuditContextAccessor auditContext,
    TimeProvider clock) : IAuditWriter
{
    public async Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId
            ?? (tenantResolver.IsResolved ? tenantResolver.TenantId : Guid.Empty);

        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Cannot write an audit entry without a resolved tenant.");

        var isAuthenticated = currentUser.IsAuthenticated;
        var actorId = isAuthenticated ? currentUser.UserId : (Guid?)null;
        var actorRole = isAuthenticated ? currentUser.Role.ToString() : null;
        var actorType = isAuthenticated ? "Staff" : (request.ActorType ?? "System");

        // Seed the chain from the tenant's most recent committed entry (Id is UUIDv7 = time-ordered).
        var prevHash = await db.AuditEntries
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        var entry = AuditEntry.Create(
            tenantId: tenantId,
            action: request.Action,
            entityType: request.EntityType,
            occurredAtUtc: clock.GetUtcNow(),
            entityId: request.EntityId,
            actorId: actorId,
            actorRole: actorRole,
            actorType: actorType,
            summary: request.Summary,
            ipAddress: auditContext.IpAddress,
            portal: request.Portal ?? auditContext.Portal,
            deviceId: currentUser.DeviceId,
            prevHash: prevHash);

        entry.SetHash(AuditHasher.ComputeHash(prevHash, entry));
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
