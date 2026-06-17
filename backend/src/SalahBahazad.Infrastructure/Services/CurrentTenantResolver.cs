using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Resolves the current tenant from the JWT claim set (FR-PLAT-TEN-002).
/// Single-tenant now; the resolver is the seam for multi-tenant expansion.
/// </summary>
internal sealed class CurrentTenantResolver(IHttpContextAccessor httpContextAccessor) : ICurrentTenantResolver
{
    private Guid? _cached;

    public Guid TenantId
    {
        get
        {
            if (_cached.HasValue) return _cached.Value;

            var claim = httpContextAccessor.HttpContext?.User
                .FindFirstValue("tenant_id");

            if (Guid.TryParse(claim, out var id))
            {
                _cached = id;
                return id;
            }

            // Fall back to the seeded default tenant for unauthenticated paths
            // (e.g. the auth exchange endpoint itself doesn't yet have a tenant claim).
            return Guid.Empty;
        }
    }

    public bool IsResolved =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id"),
            out _);
}
