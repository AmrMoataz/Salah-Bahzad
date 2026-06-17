using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.RateLimiting;
using SalahBahazad.Api.Endpoints;
using SalahBahazad.Application.Common.Behaviors;
using SalahBahazad.Infrastructure;
using SalahBahazad.ServiceDefaults;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Threading.RateLimiting;

// ── Bootstrap logger (captures startup errors) ────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Aspire service defaults (OTel traces+metrics, health checks) ──────────
    builder.AddServiceDefaults();

    // ── Serilog (NFR-OBS-001, NFR-PRIV-005) ─────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

    // ── Infrastructure (EF, Firebase, JWT, Redis, Auth) ─────────────────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── CQRS — source-generated Mediator ────────────────────────────────────
    // Handlers are Scoped so they can consume scoped services (e.g. IAppDbContext / EF Core).
    builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

    // ── FluentValidation pipeline ────────────────────────────────────────────
    builder.Services.AddValidatorsFromAssembly(
        typeof(SalahBahazad.Application.Features.Auth.PermissionCatalog).Assembly);

    builder.Services.AddTransient(
        typeof(IPipelineBehavior<,>),
        typeof(LoggingBehavior<,>));

    builder.Services.AddTransient(
        typeof(IPipelineBehavior<,>),
        typeof(ValidationBehavior<,>));

    // ── ProblemDetails (RFC 7807) ────────────────────────────────────────────
    builder.Services.AddProblemDetails();

    // ── OpenAPI / Scalar ─────────────────────────────────────────────────────
    builder.Services.AddOpenApi();

    // ── Rate limiting (NFR-SEC-006) ───────────────────────────────────────────
    builder.Services.AddRateLimiter(opts =>
    {
        opts.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.PermitLimit = 10;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiter.QueueLimit = 0;
        });

        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // ── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddCors(opts =>
    {
        opts.AddPolicy("AdminPortal", policy =>
        {
            var origins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? ["http://localhost:4200"];

            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // ── Build ────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(opts =>
        {
            opts.Title = "Salah Bahzad API";
            opts.Theme = ScalarTheme.Default;
        });
    }

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging(opts =>
    {
        opts.GetLevel = (ctx, _, ex) =>
            ex is not null
                ? LogEventLevel.Error
                : ctx.Response.StatusCode >= 500
                    ? LogEventLevel.Error
                    : LogEventLevel.Information;
    });

    app.UseCors("AdminPortal");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // ── Endpoints ────────────────────────────────────────────────────────────
    app.MapAuthEndpoints();
    app.MapHealthEndpoints();

    // ── Bootstrap seed (Development only) ─────────────────────────────────────
    // Migrations are gated (no auto-apply on prod boot), so seeding assumes the schema
    // already exists. A seed failure is logged but never blocks startup.
    if (app.Environment.IsDevelopment())
    {
        try
        {
            await SalahBahazad.Infrastructure.Persistence.DatabaseSeeder.SeedAsync(app.Services);
        }
        catch (Exception seedEx)
        {
            Log.Warning(seedEx, "Database seed failed (continuing startup)");
        }
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
