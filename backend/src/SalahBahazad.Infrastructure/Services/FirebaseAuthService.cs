using FirebaseAdmin.Auth;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Wraps the Firebase Admin SDK to verify Firebase ID tokens server-side.
/// Firebase is the IdP; the platform issues its own JWT after verification (FR-PLAT-AUTH-002).
/// </summary>
internal sealed class FirebaseAuthService : IFirebaseAuthService
{
    public async Task<FirebaseTokenClaims> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        FirebaseToken decoded;
        try
        {
            decoded = await FirebaseAuth.DefaultInstance
                .VerifyIdTokenAsync(idToken, cancellationToken);
        }
        catch (FirebaseAuthException ex)
        {
            throw new UnauthorizedAccessException(
                "Firebase token verification failed.", ex);
        }

        decoded.Claims.TryGetValue("email", out var emailObj);
        decoded.Claims.TryGetValue("name", out var nameObj);

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(decoded.IssuedAtTimeSeconds);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(decoded.ExpirationTimeSeconds);

        return new FirebaseTokenClaims(
            Uid: decoded.Uid,
            Email: emailObj?.ToString() ?? string.Empty,
            DisplayName: nameObj?.ToString(),
            IssuedAt: issuedAt,
            ExpiresAt: expiresAt);
    }
}
