namespace SalahBahazad.Domain.Enums;

/// <summary>
/// How a <see cref="Entities.PaymentTransaction"/> was settled (FR-PLAT-PAY-001/002). Only the offline
/// seam ships in Phase 4; an online <c>Gateway</c> value is reserved for later.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Settled by redeeming a code (the code's value is the amount).</summary>
    CodeRedemption = 0,

    /// <summary>Staff unlock — recorded at amount 0 (access granted, no charge).</summary>
    Unlock = 1,
}
