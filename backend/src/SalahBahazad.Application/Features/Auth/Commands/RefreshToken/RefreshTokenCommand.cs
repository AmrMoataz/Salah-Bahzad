using Mediator;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Exchanges a still-valid platform refresh token for a fresh access+refresh pair
/// without a round-trip to Firebase, so an active staff session survives access-token
/// expiry (FR-PLAT-AUTH-002: "Refresh + revocation supported"). A new refresh token is
/// issued on every call, giving the session a sliding lifetime.
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokenResponse>;
