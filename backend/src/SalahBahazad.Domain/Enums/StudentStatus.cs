namespace SalahBahazad.Domain.Enums;

/// <summary>
/// Lifecycle state of a student account (FR-ADM-STU-001..006, FR-STU-REG-008).
/// Self-registration creates a <see cref="Pending"/> student; staff move it to
/// <see cref="Active"/> (approve) or <see cref="Rejected"/> (reject-with-reason),
/// and may later <see cref="Inactive"/> (deactivate) / re-activate.
/// </summary>
public enum StudentStatus
{
    /// <summary>Newly self-registered; awaiting staff review. Cannot sign in yet (FR-STU-REG-008).</summary>
    Pending = 0,

    /// <summary>Approved and able to sign in / enrol (FR-ADM-STU-003).</summary>
    Active = 1,

    /// <summary>Registration was declined with a mandatory reason (FR-ADM-STU-004).</summary>
    Rejected = 2,

    /// <summary>Previously active, now disabled by staff; sign-in is refused (FR-ADM-STU-006).</summary>
    Inactive = 3,
}
