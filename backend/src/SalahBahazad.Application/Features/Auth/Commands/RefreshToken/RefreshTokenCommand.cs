using Mediator;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Exchanges a still-valid platform refresh token for a fresh access+refresh pair without a round-trip to
/// Firebase, so an active session survives access-token expiry (FR-PLAT-AUTH-002: "Refresh + revocation
/// supported"). A new refresh token is issued on every call, giving the session a sliding lifetime.
/// <b>Role-aware</b> (S0): one endpoint serves staff and students — a <c>role=Student</c> refresh token
/// reloads the <c>Student</c> and reissues a student pair preserving its <c>device_id</c>.
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshResult>;

/// <summary>
/// Discriminated result of a role-aware refresh: exactly one of <see cref="Staff"/> / <see cref="Student"/>
/// is set. The endpoint returns whichever is present, so the wire shape is unchanged from each client's
/// perspective (staff → <see cref="AuthTokenResponse"/>, student → <see cref="StudentAuthResponse"/>);
/// this wrapper itself is never serialised.
/// </summary>
public sealed record RefreshResult(AuthTokenResponse? Staff, StudentAuthResponse? Student);
