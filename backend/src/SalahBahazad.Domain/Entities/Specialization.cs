using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A teacher-managed specialization (e.g. "Mechanics") that belongs to exactly one
/// <see cref="Subject"/> (FR-PLAT-TAX-002). Tenant-scoped, dynamic, and soft-deleted so
/// historical Session references survive (FR-PLAT-ROLE-004).
/// </summary>
public sealed class Specialization : TenantEntityBase, ISoftDeletable
{
    private Specialization() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Owning subject — a specialization belongs to exactly one subject (FR-PLAT-TAX-002).</summary>
    public Guid SubjectId { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Specialization Create(Guid tenantId, Guid subjectId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (subjectId == Guid.Empty)
            throw new ArgumentException("A specialization must belong to a subject.", nameof(subjectId));

        var specialization = new Specialization { Name = name.Trim(), SubjectId = subjectId };
        specialization.SetTenant(tenantId);
        return specialization;
    }

    /// <summary>Renames and/or reassigns the specialization to another subject (FR-ADM-TAX-001).</summary>
    public void Update(string name, Guid subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (subjectId == Guid.Empty)
            throw new ArgumentException("A specialization must belong to a subject.", nameof(subjectId));

        Name = name.Trim();
        SubjectId = subjectId;
    }

    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
    }
}
