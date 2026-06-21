using Mediator;

namespace SalahBahazad.Application.Features.Videos.Queries.GetHlsKey;

/// <summary>
/// Returns the raw 16-byte AES-128 content key for a video (contract §B #3, FR-PLAT-VID-003) — the endpoint the
/// manifest's <c>#EXT-X-KEY</c> URI points at. Re-runs the gate's authorisation subset (active enrollment + quiz
/// passed) but does <b>not</b> decrement: an HLS client legitimately re-fetches the key during one sitting.
/// </summary>
public sealed record GetHlsKeyQuery(Guid VideoId) : IRequest<byte[]>;
