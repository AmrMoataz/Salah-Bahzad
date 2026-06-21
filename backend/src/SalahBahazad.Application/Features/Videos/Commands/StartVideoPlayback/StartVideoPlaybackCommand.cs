using Mediator;
using SalahBahazad.Application.Features.Videos.DTOs;

namespace SalahBahazad.Application.Features.Videos.Commands.StartVideoPlayback;

/// <summary>
/// The video playback gate (contract §B #1, FR-PLAT-VID-001/002/006): authorises the calling student for a
/// video (active+unexpired enrollment, quiz passed if gated, views remaining), spends one view, audits the
/// start, and returns a one-time handoff code. Not <c>ITransactionalRequest</c> — the handler manages its own
/// transaction so the handoff code is minted only <i>after</i> the decrement durably commits.
/// </summary>
public sealed record StartVideoPlaybackCommand(Guid VideoId) : IRequest<PlaybackHandoffDto>;
