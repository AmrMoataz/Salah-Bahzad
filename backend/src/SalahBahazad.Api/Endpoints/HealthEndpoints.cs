using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Liveness and readiness probes for orchestrators (NFR-AUD-004, NFR-AVAIL-001).
/// </summary>
internal static class HealthEndpoints
{
    internal static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow }))
            .WithTags("Health")
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/readyz", async (IServiceProvider sp) =>
        {
            // Could add DB + Redis checks here later
            return Results.Ok(new { status = "ready", utc = DateTimeOffset.UtcNow });
        })
            .WithTags("Health")
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }
}
