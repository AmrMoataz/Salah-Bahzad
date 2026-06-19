using Mediator;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMaterialDownloadUrl;

/// <summary>Issues a short-lived signed URL for a session material's preview/download button
/// (FR-ADM-SES-004, FR-PLAT-AST-003).</summary>
public sealed record GetMaterialDownloadUrlQuery(Guid SessionId, Guid MaterialId) : IRequest<SignedUrlDto>;
