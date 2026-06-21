namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Mints and verifies the long-lived, server-signed device token that binds a student to a single
/// device (FR-PLAT-DEV-001/003/005). The raw token is a credential and lives only in the HttpOnly
/// <c>sb_device</c> cookie; the database stores only its <b>hash</b>
/// (<see cref="Domain.Entities.StudentDevice.DeviceTokenHash"/>) so a database leak cannot replay it.
/// The fingerprint header is a secondary, human-readable signal for staff visibility (FR-PLAT-DEV-006).
/// </summary>
public interface IDeviceBindingService
{
    /// <summary>
    /// Issues a fresh device token for a (student, device) pair. Returns the opaque raw token (for the
    /// cookie) and the hash to persist. <paramref name="deviceGuid"/> is entropy bound into the token —
    /// it need not equal the resulting <see cref="Domain.Entities.StudentDevice"/> id.
    /// </summary>
    (string RawToken, string Hash) Issue(Guid studentId, Guid deviceGuid);

    /// <summary>
    /// Validates the signature of a presented raw token and, if authentic, returns the hash to compare
    /// against the stored <see cref="Domain.Entities.StudentDevice.DeviceTokenHash"/>. A malformed or
    /// forged (wrong-key) token returns <c>null</c> so the caller can reject it as an unrecognised device.
    /// </summary>
    string? Verify(string rawToken);

    /// <summary>Normalises the client-supplied fingerprint into a trimmed summary (null when blank).</summary>
    string? Summarize(string? fingerprint);
}
