using System.Net.Http.Json;
using FirebaseAdmin.Auth;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Wraps the Firebase Admin SDK to verify Firebase ID tokens server-side.
/// Firebase is the IdP; the platform issues its own JWT after verification (FR-PLAT-AUTH-002).
/// </summary>
internal sealed class FirebaseAuthService(IHttpClientFactory httpClientFactory, string webApiKey)
    : IFirebaseAuthService
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

    public async Task<string> CreateUserAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await FirebaseAuth.DefaultInstance.CreateUserAsync(
                new UserRecordArgs
                {
                    Email = email,
                    DisplayName = displayName,
                    EmailVerified = false,
                },
                cancellationToken);

            return user.Uid;
        }
        catch (FirebaseAuthException ex)
        {
            throw new InvalidOperationException(
                "Could not create the Firebase account for this staff member.", ex);
        }
    }

    public async Task SetUserDisabledAsync(
        string firebaseUid,
        bool disabled,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await FirebaseAuth.DefaultInstance.UpdateUserAsync(
                new UserRecordArgs { Uid = firebaseUid, Disabled = disabled },
                cancellationToken);
        }
        catch (FirebaseAuthException ex)
        {
            throw new InvalidOperationException(
                "Could not update the Firebase account state for this staff member.", ex);
        }
    }

    public async Task SendPasswordResetEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        // The Admin SDK's GeneratePasswordResetLink only builds the URL — it never sends an email.
        // To have Firebase deliver its own templated reset email we call the Identity Toolkit
        // REST endpoint accounts:sendOobCode, which requires the project's Web API key.
        var client = httpClientFactory.CreateClient();
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={webApiKey}";

        using var response = await client.PostAsJsonAsync(
            url,
            new { requestType = "PASSWORD_RESET", email },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Firebase rejected the password-reset request for this staff member ({(int)response.StatusCode}): {body}");
        }
    }
}
