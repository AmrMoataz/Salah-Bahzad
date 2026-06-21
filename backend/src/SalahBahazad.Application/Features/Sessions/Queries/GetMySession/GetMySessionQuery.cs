using Mediator;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMySession;

/// <summary>
/// The student-portal session-detail read (S3, contract §B): the full study view for one <b>enrolled</b> session —
/// header, progress, gate banner, the ordered video playlist with per-video lock state, materials (names only),
/// and the assignment + quiz entry status. <paramref name="SessionId"/> is a session id; ownership is the caller's
/// own non-refunded enrollment, so a non-enrolled / cross-tenant / refunded id resolves to 404 (the IDOR boundary,
/// NFR-SEC-007). The student + tenant come from the JWT.
/// </summary>
public sealed record GetMySessionQuery(Guid SessionId) : IRequest<MySessionDetailDto>;
