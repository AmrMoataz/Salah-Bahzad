namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>Verifies Firebase ID tokens and manages staff Firebase accounts (the platform stores no passwords).</summary>
public interface IFirebaseAuthService
{
    /// <summary>
    /// Verifies the Firebase ID token and returns the decoded claims.
    /// Throws if the token is invalid or expired.
    /// </summary>
    Task<FirebaseTokenClaims> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a Firebase account for a new staff member and returns its UID.
    /// The user sets their own password via the self-service reset flow (FR-ADM-STAFF-002).
    /// </summary>
    Task<string> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a Firebase account so a deactivated/removed staff member cannot obtain tokens.
    /// </summary>
    Task SetUserDisabledAsync(string firebaseUid, bool disabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks Firebase to send its templated password-reset email to the given address
    /// (FR-ADM-STAFF-002 / FR-PLAT-AUTH-009). Delivery is handled entirely by Firebase —
    /// no app-side email logic.
    /// </summary>
    Task SendPasswordResetEmailAsync(string email, CancellationToken cancellationToken = default);
}

public sealed record FirebaseTokenClaims(
    string Uid,
    string Email,
    string? DisplayName,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
