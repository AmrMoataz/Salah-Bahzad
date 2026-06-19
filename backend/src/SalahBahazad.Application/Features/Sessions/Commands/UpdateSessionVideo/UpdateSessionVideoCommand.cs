using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateSessionVideo;

/// <summary>
/// Edits a video's metadata and, when a new file is supplied, replaces the source and re-enqueues
/// transcoding (FR-ADM-SES-003). The file fields are null on a metadata-only edit.
///
/// Not <see cref="ITransactionalRequest"/>: a replacement upload can be multi-GB and must not hold a
/// database transaction open. The handler streams the new source first (when present), then wraps only
/// the fast DB writes in an explicit transaction.
/// </summary>
public sealed record UpdateSessionVideoCommand(
    Guid SessionId,
    Guid VideoId,
    string Title,
    int LengthMinutes,
    int AccessCount,
    Stream? Content,
    string? ContentType,
    long? Length,
    string? FileName) : IRequest<SessionVideoDto>
{
    public bool HasNewSource => Content is not null;
}
