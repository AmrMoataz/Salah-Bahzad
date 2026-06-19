namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Supplies request-scoped audit context (the "where" of an action) that is not part of the
/// JWT identity — IP address and originating portal. Implemented in Infrastructure from the
/// current <c>HttpContext</c>; consumed by the audit interceptor so every <c>AuditEntry</c>
/// carries the full <c>Ip</c>/<c>Portal</c> set required by FR-PLAT-AUD-001.
/// </summary>
public interface IAuditContextAccessor
{
    /// <summary>Caller IP address (null outside an HTTP request, e.g. background jobs).</summary>
    string? IpAddress { get; }

    /// <summary>Originating portal: "admin" | "student" | "system" (from the <c>X-Portal</c> header).</summary>
    string? Portal { get; }
}
