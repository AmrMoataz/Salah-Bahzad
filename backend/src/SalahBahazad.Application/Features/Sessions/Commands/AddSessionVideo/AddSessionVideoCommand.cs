using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.AddSessionVideo;

/// <summary>
/// Uploads a source video to R2, appends it to the session in <c>Pending</c>, and enqueues transcoding
/// (FR-ADM-SES-003, FR-PLAT-VID-007). The run length is computed by the transcode pipeline (ffprobe), not
/// supplied here; the 5C gate issues playback/HLS.
///
/// Not <see cref="ITransactionalRequest"/>: the (potentially multi-GB) R2 upload must not hold a database
/// transaction open. The handler streams the source first, then wraps only the fast DB writes in an
/// explicit transaction.
/// </summary>
public sealed record AddSessionVideoCommand(
    Guid SessionId,
    string Title,
    int AccessCount,
    Stream Content,
    string ContentType,
    long Length,
    string FileName) : IRequest<SessionVideoDto>
{
    public static readonly string[] AllowedContentTypes = ["video/mp4", "video/quicktime", "video/webm", "video/matroska"];

    /// <summary>Maximum source video size (2 GB).</summary>
    public const long MaxBytes = 2L * 1024 * 1024 * 1024;
}
