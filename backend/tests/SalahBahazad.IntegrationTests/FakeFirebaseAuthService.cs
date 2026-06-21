using System.Collections.Concurrent;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Test double for <see cref="IFirebaseAuthService"/> — no network, deterministic results.
/// Lets the auth endpoints run end-to-end without real Firebase credentials. By default every
/// verification yields a fresh random UID (so registration creates a new account); <see cref="Pin"/>
/// maps a specific raw id token to a known UID so sign-in tests can resolve a seeded student.
/// </summary>
internal sealed class FakeFirebaseAuthService : IFirebaseAuthService
{
    private readonly ConcurrentDictionary<string, FirebaseTokenClaims> _pinned = new(StringComparer.Ordinal);

    /// <summary>Pins the claims returned for <paramref name="idToken"/> to a known <paramref name="firebaseUid"/>.</summary>
    public void Pin(string idToken, string firebaseUid, string email = "student@example.com") =>
        _pinned[idToken] = new FirebaseTokenClaims(
            Uid: firebaseUid,
            Email: email,
            DisplayName: "Fake Student",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    public Task<FirebaseTokenClaims> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(_pinned.TryGetValue(idToken ?? string.Empty, out var claims)
            ? claims
            : new FirebaseTokenClaims(
                Uid: $"fake-{Guid.NewGuid():N}",
                Email: "fake@example.com",
                DisplayName: "Fake",
                IssuedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));

    public Task<string> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken = default) =>
        Task.FromResult($"fake-uid-{Guid.NewGuid():N}");

    public Task SetUserDisabledAsync(string firebaseUid, bool disabled, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendPasswordResetEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
