using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Resolves the current tenant from the JWT claim set (FR-PLAT-TEN-002).
/// Single-tenant now; the resolver is the seam for multi-tenant expansion.
/// </summary>
/// <remarks>
/// Must NOT cache the resolved tenant. EF Core builds and caches the model (including the global
/// query filter) once per context type, capturing whichever scoped resolver instance first triggers
/// the model build via <c>Expression.Constant(tenantResolver)</c>. That single instance is then reused
/// for every request's filtered queries, so any per-instance cache would freeze the first request's
/// tenant and leak it across tenants (breaking isolation — NFR-SEC-010). Reading the claim fresh each
/// time is correct because <see cref="IHttpContextAccessor"/> is a singleton backed by an AsyncLocal,
/// so it always reflects the current request even through the captured instance.
/// </remarks>
internal sealed class CurrentTenantResolver(IHttpContextAccessor httpContextAccessor) : ICurrentTenantResolver
{
    // Fall back to Guid.Empty for unauthenticated paths (e.g. the auth exchange endpoint, which has
    // no tenant claim yet); a tenant-owned query under Guid.Empty simply matches nothing.
    public Guid TenantId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id"), out var id)
            ? id
            : Guid.Empty;

    public bool IsResolved =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id"), out _);
}
