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
internal sealed class CurrentTenantResolver(
    IHttpContextAccessor httpContextAccessor, ISystemOperationContext systemOperation) : ICurrentTenantResolver
{
    // A System operation (Hangfire job / hub callback) supplies the tenant explicitly — there is no request
    // to read the claim from (FR-PLAT-QZ-004/005). Otherwise fall back to the JWT claim, then Guid.Empty for
    // unauthenticated paths (e.g. the auth exchange endpoint); a tenant-owned query under Empty matches nothing.
    public Guid TenantId
    {
        get
        {
            if (systemOperation.Current is { } operation)
                return operation.TenantId;

            return Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id"), out var id)
                ? id
                : Guid.Empty;
        }
    }

    public bool IsResolved =>
        systemOperation.Current is not null
        || Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id"), out _);
}
