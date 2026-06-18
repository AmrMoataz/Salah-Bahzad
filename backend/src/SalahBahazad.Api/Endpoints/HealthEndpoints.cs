namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Liveness and readiness probes for orchestrators (NFR-AUD-004, NFR-AVAIL-001).
/// </summary>
internal sealed class HealthEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", (TimeProvider clock) =>
                Results.Ok(new { status = "healthy", utc = clock.GetUtcNow() }))
            .WithTags("Health")
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/readyz", (TimeProvider clock) =>
                // Could add DB + Redis checks here later
                Results.Ok(new { status = "ready", utc = clock.GetUtcNow() }))
            .WithTags("Health")
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
