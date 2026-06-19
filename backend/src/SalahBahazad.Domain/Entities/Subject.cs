using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A teacher-managed subject (e.g. "Physics"), tenant-scoped and dynamic (FR-PLAT-TAX-001).
/// Parent of <see cref="Specialization"/> in the Subject → Specialization → Session hierarchy
/// (FR-PLAT-TAX-002). Soft-deleted so historical references survive (FR-PLAT-ROLE-004).
/// </summary>
public sealed class Subject : TenantEntityBase, ISoftDeletable
{
    private Subject() { }

    public string Name { get; private set; } = string.Empty;

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Subject Create(Guid tenantId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var subject = new Subject { Name = name.Trim() };
        subject.SetTenant(tenantId);
        return subject;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
    }
}
