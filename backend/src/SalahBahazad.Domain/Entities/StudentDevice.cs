using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A device bound to a student. Each student has at most one <see cref="IsActive"/> device
/// (FR-PLAT-DEV-001); cleared devices are retained as history rather than deleted (FR-PLAT-DEV-006),
/// so this entity is <b>not</b> soft-deletable — IsActive carries the lifecycle.
/// The consent/binding/fingerprint flow itself is student-portal behaviour (FR-PLAT-DEV-002/003/005)
/// and is built later behind <c>IDeviceBindingService</c>; this entity is the server-side seam plus
/// the staff clear/visibility surface (FR-PLAT-DEV-004/006).
/// </summary>
public sealed class StudentDevice : TenantEntityBase
{
    private StudentDevice() { }

    public Guid StudentId { get; private set; }

    /// <summary>
    /// Hash of the server-issued device token (FR-PLAT-DEV-005) — never the raw token, which is a
    /// credential. Combined with <see cref="FingerprintSummary"/> as a secondary signal.
    /// </summary>
    public string DeviceTokenHash { get; private set; } = string.Empty;

    /// <summary>Human-readable fingerprint summary (e.g. OS / browser) for staff visibility (FR-PLAT-DEV-006).</summary>
    public string? FingerprintSummary { get; private set; }

    public DateTimeOffset BoundAtUtc { get; private set; }

    public bool IsActive { get; private set; } = true;

    // Clear audit (who/when/why) — FR-PLAT-DEV-004
    public DateTimeOffset? ClearedAtUtc { get; private set; }
    public Guid? ClearedById { get; private set; }
    public string? ClearReason { get; private set; }

    /// <summary>
    /// Binds a new active device to a student (FR-PLAT-DEV-001). Called by the device-binding seam
    /// after the student-portal consent flow; the caller is responsible for first clearing any
    /// existing active device so the "one active device" invariant holds.
    /// </summary>
    public static StudentDevice Bind(
        Guid tenantId,
        Guid studentId,
        string deviceTokenHash,
        string? fingerprintSummary,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceTokenHash);
        if (studentId == Guid.Empty)
            throw new ArgumentException("A device must belong to a student.", nameof(studentId));

        var device = new StudentDevice
        {
            StudentId = studentId,
            DeviceTokenHash = deviceTokenHash,
            FingerprintSummary = string.IsNullOrWhiteSpace(fingerprintSummary) ? null : fingerprintSummary.Trim(),
            BoundAtUtc = now,
            IsActive = true,
        };
        device.SetTenant(tenantId);
        return device;
    }

    /// <summary>
    /// Staff clears the device, with a mandatory reason; the next sign-in may re-bind (FR-PLAT-DEV-004).
    /// Idempotent-safe guard: an already-cleared device cannot be cleared again.
    /// </summary>
    public void Clear(Guid actorId, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!IsActive)
            throw new InvalidOperationException("This device has already been cleared (FR-PLAT-DEV-004).");

        IsActive = false;
        ClearedAtUtc = now;
        ClearedById = actorId;
        ClearReason = reason.Trim();
        AddDomainEvent(new StudentDeviceClearedEvent(StudentId, Id, ClearReason));
    }
}
