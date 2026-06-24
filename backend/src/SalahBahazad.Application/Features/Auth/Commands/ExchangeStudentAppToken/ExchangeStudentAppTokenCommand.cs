using Mediator;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentAppToken;

/// <summary>
/// Verifies a Firebase ID token from the <b>native app</b> and exchanges it for a Student-role platform
/// JWT pair, enforcing only the status gate (FR-PLAT-AUTH-005). Unlike the portal
/// <c>ExchangeStudentFirebaseTokenCommand</c>, this path is <b>device-agnostic</b>: it binds no device,
/// reads no device cookie/fingerprint, and the issued tokens carry <b>no</b> <c>device_id</c> — an
/// <c>Active</c> student may sign in to the app on any machine (contract §A, FR-APP-AUTH-001/FR-APP-DEV-001).
/// Returns <see cref="StudentAuthResponse"/> directly (no device-token wrapper) with <c>BoundDevice = null</c>.
/// </summary>
public sealed record ExchangeStudentAppTokenCommand(string FirebaseIdToken)
    : IRequest<StudentAuthResponse>;
