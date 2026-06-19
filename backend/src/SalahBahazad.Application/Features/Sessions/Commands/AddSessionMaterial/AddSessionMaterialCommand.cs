using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.AddSessionMaterial;

/// <summary>Uploads a readable material (PDF/CSV/PNG/JPG) to R2 and attaches it to the session
/// (FR-PLAT-SES-003, FR-ADM-SES-004).</summary>
public sealed record AddSessionMaterialCommand(
    Guid SessionId,
    Stream Content,
    string ContentType,
    long Length,
    string FileName) : IRequest<SessionMaterialDto>, ITransactionalRequest
{
    public static readonly string[] AllowedContentTypes =
        ["application/pdf", "text/csv", "image/png", "image/jpeg"];

    /// <summary>Maximum material size (25 MB).</summary>
    public const long MaxBytes = 25 * 1024 * 1024;
}
