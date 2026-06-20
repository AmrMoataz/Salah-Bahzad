using Mediator;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Audit.DTOs;

namespace SalahBahazad.Application.Features.Audit.Queries.ListAudit;

/// <summary>
/// The activity-log feed (contract #1, FR-ADM-AUD-001..003, scrActivity): actor/action/target rows filtered by
/// the design's actor + category + period bar, paged, newest first. Also powers the per-student/per-session
/// Activity tabs via <see cref="StudentId"/>/<see cref="SessionId"/>/<see cref="EntityType"/>/<see cref="EntityId"/>.
/// <para>
/// <see cref="IncludeSensitive"/> is supplied by the endpoint from the caller's <c>AuditReadSensitive</c>
/// permission (contract §0/§4) — never derived in the handler. When false, "who-read-what" rows
/// (<see cref="SensitiveAuditActions"/>) are excluded (the prototype's "Scoped view" for Assistants).
/// </para>
/// </summary>
public sealed record ListAuditQuery(
    Guid? ActorId = null,
    string? ActorType = null,
    string? Category = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Period = null,
    Guid? StudentId = null,
    Guid? SessionId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    int Page = 1,
    int PageSize = 20,
    bool IncludeSensitive = false) : IRequest<PagedResult<AuditFeedItem>>;
