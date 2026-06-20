namespace SalahBahazad.Domain.Enums;

/// <summary>
/// How an <see cref="Entities.Enrollment"/> was granted: by a student redeeming a <see cref="Code"/>
/// (FR-PLAT-ENR-001) or by staff unlocking it directly, bypassing code &amp; price (FR-PLAT-ENR-002).
/// </summary>
public enum EnrollmentMethod
{
    /// <summary>Student redeemed a code whose value matched the session price (#12).</summary>
    Code = 0,

    /// <summary>Staff granted access directly (#9), no code, amount 0.</summary>
    Unlock = 1,
}
