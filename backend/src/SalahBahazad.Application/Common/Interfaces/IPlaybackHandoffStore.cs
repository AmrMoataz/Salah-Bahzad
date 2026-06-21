namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Issues and consumes the short-lived, single-use <b>handoff code</b> the web→app deep link carries instead
/// of a raw token (FR-PLAT-VID-005). Backed by Redis so the code is atomically consumable across instances.
/// The playback gate issues a code after authorising + decrementing; the native app exchanges it (once) at
/// redeem for the signed manifest.
/// </summary>
public interface IPlaybackHandoffStore
{
    /// <summary>Stores <paramref name="handoff"/> under a fresh random code with the given TTL; returns the code.</summary>
    Task<string> IssueAsync(PlaybackHandoff handoff, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reads-and-deletes the handoff for <paramref name="code"/> (single-use). Returns null when the
    /// code is unknown, already consumed, or expired.
    /// </summary>
    Task<PlaybackHandoff?> ConsumeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>The authorised playback grant a handoff code stands in for.</summary>
public sealed record PlaybackHandoff(Guid VideoId, Guid EnrollmentId, Guid StudentId, Guid TenantId);
