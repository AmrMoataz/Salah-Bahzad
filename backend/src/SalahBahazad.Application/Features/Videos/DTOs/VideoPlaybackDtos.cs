namespace SalahBahazad.Application.Features.Videos.DTOs;

/// <summary>
/// The playback-gate result (FR-PLAT-VID-005): a single-use code the device-aware deep link carries — never a
/// raw URL or token. The native app exchanges it once at redeem for the signed manifest.
/// </summary>
public sealed record PlaybackHandoffDto(string HandoffCode, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// The redeem result (FR-PLAT-VID-003). <see cref="ManifestContent"/> is a per-playback <c>.m3u8</c> whose
/// segment URIs are short-lived signed R2 URLs and whose <c>#EXT-X-KEY</c> URI is <see cref="KeyUrl"/> (the
/// gated key endpoint). <see cref="ExpiresAtUtc"/> is the soonest signed-segment expiry.
/// </summary>
public sealed record PlaybackManifestDto(string ManifestContent, string KeyUrl, DateTimeOffset ExpiresAtUtc);
