using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Audit.DTOs;
using SalahBahazad.Application.Features.Audit.Queries.ListAudit;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Activity-log feed endpoint (contract #1, FR-ADM-AUD-001..003). <c>AuditRead</c>, default-deny. The feed and
/// the per-entity Activity tabs share this route; callers without <c>AuditReadSensitive</c> get the
/// Assistant-scoped view (the "who-read-what" rows are filtered out — contract §4).
/// </summary>
internal sealed class AuditEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .WithOpenApi();

        group.MapGet("/", ListAsync)
            .RequirePermission(Permission.AuditRead)
            .WithName("ListAudit")
            .WithSummary("Activity feed filtered by actor/type/category/period (or from-to) and entity, paged")
            .Produces<PagedResult<AuditFeedItem>>();
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] Guid? actorId = null,
        [FromQuery] string? actorType = null,
        [FromQuery] string? category = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? period = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] Guid? sessionId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Compute the sensitive scope HERE from the caller's permission (contract §0/§4) — not in the handler.
        var includeSensitive = http.HasPermission(Permission.AuditReadSensitive);
        return Results.Ok(await sender.Send(
            new ListAuditQuery(actorId, actorType, category, from, to, period,
                studentId, sessionId, entityType, entityId, page, pageSize, includeSensitive),
            cancellationToken));
    }
}
