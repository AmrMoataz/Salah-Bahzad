using System.Security.Cryptography;
using System.Text;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence;

/// <summary>
/// Shared SHA-256 hash-chain computation for <see cref="AuditEntry"/> rows, used by both the audit
/// <c>SaveChangesInterceptor</c> (automatic field-diff entries) and the explicit <c>IAuditWriter</c>
/// (read-access / anonymous entries) so every entry — however created — links into the same
/// tamper-evident chain (NFR-AUD-001). Any tampering changes a hash and breaks every downstream link.
/// </summary>
internal static class AuditHasher
{
    public static string ComputeHash(string? prevHash, AuditEntry entry)
    {
        var payload = string.Join('|',
            prevHash ?? string.Empty,
            entry.TenantId,
            entry.Action,
            entry.EntityType,
            entry.EntityId?.ToString() ?? string.Empty,
            entry.ActorId?.ToString() ?? string.Empty,
            entry.ActorType,
            entry.OccurredAtUtc.UtcDateTime.ToString("O"),
            entry.BeforeJson ?? string.Empty,
            entry.AfterJson ?? string.Empty);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }
}
