namespace SalahBahazad.Domain.Common;

/// <summary>
/// Base for tenant-owned root entities. Adds <see cref="TenantId"/> to <see cref="EntityBase"/>
/// so the EF global query filter can enforce isolation (FR-PLAT-TEN-001/003).
/// </summary>
public abstract class TenantEntityBase : EntityBase, ITenantOwned
{
    /// <summary>Owning tenant. Set once when the entity is created within a tenant context.</summary>
    public Guid TenantId { get; protected set; }

    /// <summary>Assigns the owning tenant. Intended for the persistence/creation path only.</summary>
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}
