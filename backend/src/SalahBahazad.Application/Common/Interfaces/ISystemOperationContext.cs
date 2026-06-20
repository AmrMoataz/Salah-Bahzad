namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Ambient context for a platform-performed operation that runs <b>outside</b> an HTTP request — a Hangfire
/// job (quiz auto-submit, FR-PLAT-QZ-005) or a SignalR hub callback (forfeit-on-disconnect, FR-PLAT-QZ-004).
/// There is no <c>HttpContext</c> to read the tenant or principal from, so a caller wraps the work in
/// <see cref="Begin"/> to supply the tenant; the tenant/user resolvers then read this instead, which lets the
/// global query filter scope correctly and the audit interceptor write the row (it skips when the tenant is
/// unresolved). While a scope is active the actor resolves to <b>System</b> (FR-PLAT-AUD-005).
/// </summary>
public interface ISystemOperationContext
{
    /// <summary>The active system operation, or null when running inside a normal request.</summary>
    SystemOperation? Current { get; }

    /// <summary>Begins a System-attributed operation for <paramref name="tenantId"/>; dispose to restore.</summary>
    IDisposable Begin(Guid tenantId);
}

/// <summary>The ambient tenant a System operation runs under.</summary>
public sealed record SystemOperation(Guid TenantId);
