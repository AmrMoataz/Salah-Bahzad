namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Resolves the current tenant from a single source (JWT claim or host header).
/// Injected into the DbContext's global query filter (FR-PLAT-TEN-001/002).
/// </summary>
public interface ICurrentTenantResolver
{
    Guid TenantId { get; }
    bool IsResolved { get; }
}
