using Mediator;
using SalahBahazad.Application.Features.Videos.DTOs;

namespace SalahBahazad.Application.Features.Videos.Queries.RedeemPlayback;

/// <summary>
/// Exchanges a one-time handoff code for a per-playback signed manifest (contract §B #2, FR-PLAT-VID-003). The
/// code is consumed atomically; a missing/expired/used/not-owner code is <c>410 handoff_expired</c>.
/// <paramref name="ApiBaseUrl"/> is supplied by the endpoint (it owns the HTTP request) so the handler can build
/// the absolute, gated key-endpoint URL written into the manifest's <c>#EXT-X-KEY</c>.
/// </summary>
public sealed record RedeemPlaybackQuery(string HandoffCode, string ApiBaseUrl) : IRequest<PlaybackManifestDto>;
