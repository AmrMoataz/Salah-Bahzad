using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Test double for <see cref="IFirebaseAuthService"/> — no network, deterministic results.
/// Lets the staff endpoints run end-to-end without real Firebase credentials.
/// </summary>
internal sealed class FakeFirebaseAuthService : IFirebaseAuthService
{
    public Task<FirebaseTokenClaims> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FirebaseTokenClaims(
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
