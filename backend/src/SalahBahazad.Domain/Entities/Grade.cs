using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A teacher-managed grade level (e.g. "Grade 10"), tenant-scoped and dynamic (FR-PLAT-TAX-001).
/// Referenced by Students and Sessions; soft-deleted so historical references survive (FR-PLAT-ROLE-004).
/// </summary>
public sealed class Grade : TenantEntityBase, ISoftDeletable
{
    private Grade() { }

    public string Name { get; private set; } = string.Empty;

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Grade Create(Guid tenantId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var grade = new Grade { Name = name.Trim() };
        grade.SetTenant(tenantId);
        return grade;
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
