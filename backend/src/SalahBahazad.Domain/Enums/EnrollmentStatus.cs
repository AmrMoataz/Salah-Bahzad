namespace SalahBahazad.Domain.Enums;

/// <summary>
/// State of a student's <see cref="Entities.Enrollment"/> in a session (FR-PLAT-ENR-001..008). A student
/// holds at most one <see cref="Active"/> enrollment per session (FR-PLAT-ENR-006). <see cref="Expired"/> is
/// reached when the validity window lapses (Phase 5 sweeps it); <see cref="Refunded"/> by staff (#10).
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>Live access to the session's content.</summary>
    Active = 0,

    /// <summary>Past its validity window; access withdrawn but history retained.</summary>
    Expired = 1,

    /// <summary>Reversed by staff; any redeemed code is returned for re-use (FR-PLAT-ENR-008).</summary>
    Refunded = 2,
}
