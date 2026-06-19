using Microsoft.AspNetCore.Http;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Reads request-scoped audit context (IP, portal) from the current <see cref="HttpContext"/>.
/// Returns nulls outside an HTTP request (e.g. Hangfire jobs, design-time), which the audit
/// interceptor records as a system action.
/// </summary>
internal sealed class AuditContextAccessor(IHttpContextAccessor httpContextAccessor) : IAuditContextAccessor
{
    public string? IpAddress =>
        httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? Portal =>
        httpContextAccessor.HttpContext?.Request.Headers["X-Portal"].FirstOrDefault();
}
