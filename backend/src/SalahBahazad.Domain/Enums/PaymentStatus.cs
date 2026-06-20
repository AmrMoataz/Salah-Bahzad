namespace SalahBahazad.Domain.Enums;

/// <summary>
/// State of a <see cref="Entities.PaymentTransaction"/> (FR-PLAT-PAY-001/002). A refund (#10) writes a
/// reversing transaction with <see cref="Refunded"/> rather than mutating the original (append-only money trail).
/// </summary>
public enum PaymentStatus
{
    /// <summary>The settling transaction recorded on enroll.</summary>
    Completed = 0,

    /// <summary>A reversing transaction recorded when the enrollment is refunded.</summary>
    Refunded = 1,
}
