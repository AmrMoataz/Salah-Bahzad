using Mediator;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListMySessions;

/// <summary>
/// The student-portal My-Sessions read (S3, contract §A, FR-STU-SES-001): the caller's <b>enrolled</b> sessions
/// (<c>Active</c> incl. past-expiry, excluding <c>Refunded</c>/soft-deleted), newest-enrolled first, each with a
/// signed thumbnail + derived progress + expiry + completion state. The optional <paramref name="State"/> chip
/// narrows the set (§A.1). The student + tenant come from the JWT (<see cref="Common.Interfaces.ICurrentUserResolver"/>),
/// never a URL id — no IDOR surface. Not paginated.
/// </summary>
public sealed record ListMySessionsQuery(MySessionState? State = null) : IRequest<IReadOnlyList<MySessionDto>>;
