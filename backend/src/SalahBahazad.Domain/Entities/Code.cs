using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A single redemption code (FR-PLAT-COD-001..006). Session-bound and value-matched: redeemable exactly once,
/// only while <see cref="CodeStatus.Active"/>, and only for <see cref="SessionId"/> when its <see cref="Value"/>
/// equals that session's current price (FR-PLAT-COD-003, contract §5). Tenant-scoped and soft-deleted so
/// financial/redemption history survives (FR-PLAT-ROLE-004).
/// <para>
/// Implements <see cref="IAuditViaEventOnly"/>: the audit interceptor records a row only when a lifecycle
/// method buffers a semantic event (disable/enable/delete/redeem), so neither the bulk mint nor the silent
/// refund-return floods the log — every action leaves exactly one entry (FR-PLAT-AUD-002).
/// </para>
/// </summary>
public sealed class Code : TenantEntityBase, ISoftDeletable, IAuditViaEventOnly
{
    private Code() { }

    /// <summary>Opaque, tenant-unique, human-keyable serial <c>SB-XXXXX-XXXXX</c> (contract §5).</summary>
    public string Serial { get; private set; } = string.Empty;

    /// <summary>The mint this code came from (provenance, FR-ADM-COD-005).</summary>
    public Guid BatchId { get; private set; }

    /// <summary>The session this code redeems for — denormalized from the batch for the register filter.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Face value (EGP); must equal the session price at redemption (FR-PLAT-COD-003).</summary>
    public decimal Value { get; private set; }

    public CodeStatus Status { get; private set; } = CodeStatus.Active;

    // ── Redemption join (set once on redeem, cleared on refund-return) ──────────
    public Guid? RedeemedByStudentId { get; private set; }
    public Guid? RedeemedEnrollmentId { get; private set; }
    public DateTimeOffset? RedeemedAtUtc { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    /// <summary>Mints a fresh <see cref="CodeStatus.Active"/> code. Called only by <see cref="CodeBatch.Generate"/>.</summary>
    internal static Code Mint(Guid tenantId, Guid batchId, Guid sessionId, decimal value, string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var code = new Code
        {
            Serial = serial,
            BatchId = batchId,
            SessionId = sessionId,
            Value = value,
            Status = CodeStatus.Active,
        };
        code.SetTenant(tenantId);
        return code;
    }

    /// <summary>Disables the code (→ <see cref="CodeStatus.Inactive"/>). A used code cannot be disabled (→409).</summary>
    public void Disable()
    {
        GuardNotUsed("disabled");
        Status = CodeStatus.Inactive;
        AddDomainEvent(new CodeDisabledEvent(Id, Serial));
    }

    /// <summary>Re-enables a disabled code (→ <see cref="CodeStatus.Active"/>). A used code cannot be enabled (→409).</summary>
    public void Enable()
    {
        GuardNotUsed("enabled");
        Status = CodeStatus.Active;
        AddDomainEvent(new CodeEnabledEvent(Id, Serial));
    }

    /// <summary>
    /// Marks the code redeemed by a student (FR-PLAT-COD-003). Only an <see cref="CodeStatus.Active"/> code may
    /// be redeemed; an illegal state is surfaced as 409 by the handler. Value/price matching and one-active-
    /// enrollment rules are enforced by the redeem handler before this call.
    /// </summary>
    public void MarkRedeemed(Guid studentId, Guid enrollmentId, DateTimeOffset now)
    {
        if (Status != CodeStatus.Active)
            throw new InvalidOperationException($"Only an active code can be redeemed; this code is {Status}.");

        Status = CodeStatus.Used;
        RedeemedByStudentId = studentId;
        RedeemedEnrollmentId = enrollmentId;
        RedeemedAtUtc = now;
        AddDomainEvent(new CodeRedeemedEvent(Id, Serial, studentId, SessionId));
    }

    /// <summary>
    /// Returns a used code to circulation when its enrollment is refunded (<c>Used → Active</c>), clearing the
    /// redemption join so it can be redeemed again (FR-PLAT-ENR-008). No event: the refund's
    /// <see cref="EnrollmentRefundedEvent"/> already names the returned serial, keeping refund to one entry.
    /// </summary>
    public void ReturnAfterRefund()
    {
        if (Status != CodeStatus.Used)
            throw new InvalidOperationException($"Only a used code can be returned; this code is {Status}.");

        Status = CodeStatus.Active;
        RedeemedByStudentId = null;
        RedeemedEnrollmentId = null;
        RedeemedAtUtc = null;
    }

    /// <summary>Soft-deletes the code (FR-PLAT-COD-004). A used code cannot be deleted (→409).</summary>
    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        GuardNotUsed("deleted");
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
        AddDomainEvent(new CodeDeletedEvent(Id, Serial));
    }

    private void GuardNotUsed(string action)
    {
        if (Status == CodeStatus.Used)
            throw new InvalidOperationException($"A used code cannot be {action} (FR-PLAT-COD-004).");
    }
}
