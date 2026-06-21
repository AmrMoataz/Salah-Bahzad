using Mediator;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentFirebaseToken;

/// <summary>
/// Verifies a Firebase ID token from the <b>student</b> portal and exchanges it for a Student-role
/// platform JWT pair, enforcing the status gate (FR-PLAT-AUTH-005) and one-device binding
/// (FR-PLAT-DEV-001/003). The separate, staff-only <c>ExchangeFirebaseTokenCommand</c> is untouched
/// (FR-STU-AUTH-001, FR-PLAT-AUTH-002).
/// </summary>
/// <param name="RawDeviceToken">The opaque token from the <c>sb_device</c> cookie, if the caller has one.</param>
/// <param name="Fingerprint">The secondary <c>X-Device-Fingerprint</c> signal (FR-PLAT-DEV-006).</param>
public sealed record ExchangeStudentFirebaseTokenCommand(
    string FirebaseIdToken,
    string? RawDeviceToken,
    string? Fingerprint,
    string? IpAddress) : IRequest<StudentExchangeResult>;

/// <summary>
/// The handler's result: the wire <see cref="StudentAuthResponse"/> plus the raw device token the
/// endpoint must write to the HttpOnly <c>sb_device</c> cookie. <see cref="DeviceTokenToSet"/> is the
/// freshly issued token on a new bind, or the re-presented token on reuse (to slide the cookie's
/// expiry); the endpoint always sets it on success and never serialises this wrapper.
/// </summary>
public sealed record StudentExchangeResult(StudentAuthResponse Response, string? DeviceTokenToSet);
