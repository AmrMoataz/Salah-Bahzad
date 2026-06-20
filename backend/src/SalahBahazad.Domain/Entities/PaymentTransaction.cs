using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// An append-only money-trail entry owned by an <see cref="Enrollment"/> (FR-PLAT-PAY-001/002). Enrol writes a
/// <see cref="PaymentStatus.Completed"/> entry; refund writes a <see cref="PaymentStatus.Refunded"/> reversal
/// rather than mutating the original. <see cref="ProviderRef"/> is reserved for the (later) online gateway and
/// is always null this phase. <see cref="IAuditViaEventOnly"/> — the enrollment's semantic event is the audit row.
/// </summary>
public sealed class PaymentTransaction : EntityBase, IAuditViaEventOnly
{
    private PaymentTransaction() { }

    public Guid EnrollmentId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>The redeemed code, when settled by code redemption; else null.</summary>
    public Guid? CodeId { get; private set; }

    public PaymentStatus Status { get; private set; }

    /// <summary>External gateway reference — always null until the online payment phase (FR-PLAT-PAY-002).</summary>
    public string? ProviderRef { get; private set; }

    internal static PaymentTransaction Completed(
        Guid enrollmentId, PaymentMethod method, decimal amount, Guid? codeId, DateTimeOffset now)
        => new()
        {
            EnrollmentId = enrollmentId,
            Method = method,
            Amount = amount,
            CodeId = codeId,
            Status = PaymentStatus.Completed,
            CreatedAtUtc = now,
        };

    internal static PaymentTransaction Reversal(
        Guid enrollmentId, PaymentMethod method, decimal amount, Guid? codeId, DateTimeOffset now)
        => new()
        {
            EnrollmentId = enrollmentId,
            Method = method,
            Amount = amount,
            CodeId = codeId,
            Status = PaymentStatus.Refunded,
            CreatedAtUtc = now,
        };
}
