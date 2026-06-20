using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A student's access grant to a session (FR-PLAT-ENR-001..008) — by redeeming a <see cref="Code"/> (#12) or a
/// staff <see cref="EnrollmentMethod.Unlock"/> (#9). Owns its per-video access counters and an append-only
/// <see cref="PaymentTransaction"/> trail. A student holds at most one <see cref="EnrollmentStatus.Active"/>
/// enrollment per session (FR-PLAT-ENR-006); re-enrolling reuses this row in place rather than duplicating it
/// (FR-PLAT-ENR-004). Tenant-scoped and soft-deleted so financial/attendance history survives (FR-PLAT-ROLE-004).
/// </summary>
public sealed class Enrollment : TenantEntityBase, ISoftDeletable
{
    private readonly List<EnrollmentVideoAccess> _videoAccesses = [];
    private readonly List<PaymentTransaction> _payments = [];

    private Enrollment() { }

    public Guid StudentId { get; private set; }
    public Guid SessionId { get; private set; }
    public EnrollmentStatus Status { get; private set; } = EnrollmentStatus.Active;
    public EnrollmentMethod Method { get; private set; }

    /// <summary>The redeemed code when <see cref="Method"/> is <see cref="EnrollmentMethod.Code"/>; else null.</summary>
    public Guid? CodeId { get; private set; }

    /// <summary>Amount settled (EGP): the code value on redeem, 0 on unlock.</summary>
    public decimal Amount { get; private set; }

    public DateTimeOffset EnrolledAtUtc { get; private set; }

    /// <summary>When access lapses; null when the session has no expiry (<c>ValidityDays == 0</c>).</summary>
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public IReadOnlyCollection<EnrollmentVideoAccess> VideoAccesses => _videoAccesses.AsReadOnly();
    public IReadOnlyCollection<PaymentTransaction> Payments => _payments.AsReadOnly();

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    /// <summary>
    /// Grants a brand-new enrollment (FR-PLAT-ENR-005): computes expiry from the session's validity, provisions
    /// one access counter per session video, records the settling payment, and raises
    /// <see cref="EnrollmentCreatedEvent"/>. The <paramref name="session"/> must have its videos loaded.
    /// </summary>
    public static Enrollment Create(
        Guid tenantId,
        Guid studentId,
        Session session,
        EnrollmentMethod method,
        Guid? codeId,
        decimal amount,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (studentId == Guid.Empty)
            throw new ArgumentException("An enrollment must belong to a student.", nameof(studentId));
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

        var enrollment = new Enrollment
        {
            StudentId = studentId,
            SessionId = session.Id,
            Status = EnrollmentStatus.Active,
            Method = method,
            CodeId = codeId,
            Amount = amount,
            EnrolledAtUtc = now,
            ExpiresAtUtc = ComputeExpiry(session.ValidityDays, now),
        };
        enrollment.SetTenant(tenantId);
        enrollment.ProvisionVideoAccess(session, reset: false);
        enrollment.RecordPayment(method, amount, codeId, now);
        enrollment.AddDomainEvent(new EnrollmentCreatedEvent(enrollment.Id, studentId, session.Id, method));
        return enrollment;
    }

    /// <summary>
    /// Re-activates this (non-active) enrollment in place for the same student+session (FR-PLAT-ENR-004): resets
    /// every video counter, adds counters for any videos added since, pushes expiry forward from now, records a
    /// fresh payment, and raises <see cref="EnrollmentExtendedEvent"/> — never a duplicate row. The
    /// <paramref name="session"/> must have its videos loaded.
    /// </summary>
    public void Extend(
        Session session, EnrollmentMethod method, Guid? codeId, decimal amount, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

        Status = EnrollmentStatus.Active;
        Method = method;
        CodeId = codeId;
        Amount = amount;
        EnrolledAtUtc = now;
        ExpiresAtUtc = ComputeExpiry(session.ValidityDays, now);
        ProvisionVideoAccess(session, reset: true);
        RecordPayment(method, amount, codeId, now);
        AddDomainEvent(new EnrollmentExtendedEvent(Id, StudentId, session.Id, method));
    }

    /// <summary>
    /// Refunds the enrollment (<c>Active → Refunded</c>, FR-PLAT-ENR-008), recording a reversing payment. Only an
    /// active enrollment can be refunded (→409). The returned code (if any) is flipped back by the handler; its
    /// <paramref name="returnedCodeSerial"/> is carried into the audit summary.
    /// </summary>
    public void Refund(DateTimeOffset now, string? returnedCodeSerial)
    {
        if (Status != EnrollmentStatus.Active)
            throw new InvalidOperationException(
                $"Only an active enrollment can be refunded; this enrollment is {Status}.");

        Status = EnrollmentStatus.Refunded;
        _payments.Add(PaymentTransaction.Reversal(Id, PaymentMethodFor(Method), Amount, CodeId, now));
        AddDomainEvent(new EnrollmentRefundedEvent(Id, StudentId, SessionId, returnedCodeSerial));
    }

    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
    }

    private void ProvisionVideoAccess(Session session, bool reset)
    {
        foreach (var video in session.Videos)
        {
            var existing = _videoAccesses.FirstOrDefault(a => a.VideoId == video.Id);
            if (existing is null)
                _videoAccesses.Add(EnrollmentVideoAccess.Create(Id, video.Id, video.AccessCount));
            else if (reset)
                existing.ResetTo(video.AccessCount);
        }
    }

    private void RecordPayment(EnrollmentMethod method, decimal amount, Guid? codeId, DateTimeOffset now)
        => _payments.Add(PaymentTransaction.Completed(Id, PaymentMethodFor(method), amount, codeId, now));

    private static PaymentMethod PaymentMethodFor(EnrollmentMethod method)
        => method == EnrollmentMethod.Code ? PaymentMethod.CodeRedemption : PaymentMethod.Unlock;

    private static DateTimeOffset? ComputeExpiry(int validityDays, DateTimeOffset now)
        => validityDays == 0 ? null : now.AddDays(validityDays);
}
