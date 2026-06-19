using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A staff member (Teacher or Assistant) within a tenant.
/// Credentials live entirely in Firebase — the platform stores no passwords (FR-PLAT-AUTH-004).
/// </summary>
public sealed class Staff : TenantEntityBase, ISoftDeletable
{
    private Staff() { }

    public string FirebaseUid { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public StaffRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Timestamp of the most recent successful sign-in; null until the member first logs in.</summary>
    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Staff Create(
        Guid tenantId,
        string firebaseUid,
        string displayName,
        string email,
        StaffRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseUid);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var staff = new Staff
        {
            FirebaseUid = firebaseUid,
            DisplayName = displayName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
        };
        staff.SetTenant(tenantId);
        return staff;
    }

    public void UpdateDetails(string displayName, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        DisplayName = displayName.Trim();
        Email = email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Self-service display-name change (Settings → Profile, FR-ADM-SET-001). The email is the
    /// Firebase sign-in identity and is intentionally not editable here.
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    public void UpdateRole(StaffRole newRole, StaffRole actorRole)
    {
        if (newRole > actorRole)
            throw new InvalidOperationException(
                "A staff member cannot be elevated to a role higher than the actor's own (FR-PLAT-ROLE-002).");

        Role = newRole;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    /// <summary>Stamps a successful sign-in, surfaced as "Last active" in the staff list (FR-ADM-STAFF-001).</summary>
    public void RecordSignIn(DateTimeOffset now) => LastSeenAtUtc = now;

    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
    }
}
