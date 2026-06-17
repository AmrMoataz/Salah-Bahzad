using Mediator;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeFirebaseToken;

/// <summary>
/// Verifies a Firebase ID token issued by the admin portal and exchanges it for a
/// short-lived platform JWT (FR-PLAT-AUTH-002, FR-ADM-AUTH-001).
/// </summary>
public sealed record ExchangeFirebaseTokenCommand(
    string FirebaseIdToken,
    string? IpAddress,
    string? DeviceId) : IRequest<AuthTokenResponse>;
