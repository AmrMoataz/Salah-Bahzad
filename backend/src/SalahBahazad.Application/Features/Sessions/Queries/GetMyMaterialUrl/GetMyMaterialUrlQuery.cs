using Mediator;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMyMaterialUrl;

/// <summary>
/// Issues a short-lived signed R2 URL for one material of the caller's <b>enrolled</b> session (student S3,
/// contract §C, FR-STU-SES-003). The session is gated by the caller's own non-refunded enrollment (404 otherwise),
/// then the material is resolved through that session (404 if not its) — IDOR/tenant-safe (NFR-SEC-007). Stays
/// available while the enrollment is Active-but-expired; not available once Refunded. Not audited.
/// </summary>
public sealed record GetMyMaterialUrlQuery(Guid SessionId, Guid MaterialId) : IRequest<SignedUrlDto>;
