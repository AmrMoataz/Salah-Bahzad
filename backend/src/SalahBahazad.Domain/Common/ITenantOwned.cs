namespace SalahBahazad.Domain.Common;

/// <summary>
/// Implemented by every tenant-scoped root entity. EF Core applies a global query filter on
/// <see cref="TenantId"/> (driven by the current-tenant resolver) so isolation is automatic —
/// never write per-handler <c>Where(x =&gt; x.TenantId == ...)</c>. See backend/CLAUDE.md and FR-PLAT-TEN-*.
/// </summary>
public interface ITenantOwned
{
    /// <summary>Owning tenant.</summary>
    Guid TenantId { get; }
}
