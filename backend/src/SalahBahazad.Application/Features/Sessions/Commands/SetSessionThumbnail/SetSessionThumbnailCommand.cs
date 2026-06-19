using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.SetSessionThumbnail;

/// <summary>
/// Uploads/replaces a session thumbnail to the private bucket and stores its object key (FR-ADM-SES-002).
/// Stored for the future student catalogue; the admin UI renders an accent tile, not the image.
/// </summary>
public sealed record SetSessionThumbnailCommand(
    Guid Id,
    Stream Content,
    string ContentType,
    long Length,
    string FileName) : IRequest<SessionDetailDto>, ITransactionalRequest
{
    public static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Maximum thumbnail size (5 MB).</summary>
    public const long MaxBytes = 5 * 1024 * 1024;
}
