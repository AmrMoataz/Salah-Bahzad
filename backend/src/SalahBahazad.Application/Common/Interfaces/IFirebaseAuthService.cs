namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>Verifies Firebase ID tokens and returns the decoded claims.</summary>
public interface IFirebaseAuthService
{
    /// <summary>
    /// Verifies the Firebase ID token and returns the decoded claims.
    /// Throws if the token is invalid or expired.
    /// </summary>
    Task<FirebaseTokenClaims> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record FirebaseTokenClaims(
    string Uid,
    string Email,
    string? DisplayName,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
