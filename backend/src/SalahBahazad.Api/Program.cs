using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.RateLimiting;
using SalahBahazad.Api.Endpoints;
using SalahBahazad.Api.Middleware;
using SalahBahazad.Application.Common.Behaviors;
using SalahBahazad.Infrastructure;
using SalahBahazad.ServiceDefaults;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text.Json.Serialization;
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

    // ── Upload limits (large source-video uploads) ──────────────────────────
    // Defaults silently reset the connection (no HTTP response) for big uploads: Kestrel caps the request
    // body at ~28.6 MB and the multipart form parser at 128 MB. Source videos are capped at 2 GB
    // (AddSessionVideoCommand.MaxBytes); allow that plus headroom for the multipart boundary + metadata
    // fields. The exact source-size cap is enforced app-side while streaming. Override via
    // Uploads:MaxRequestBodyBytes.
    var maxUploadBytes = builder.Configuration.GetValue<long?>("Uploads:MaxRequestBodyBytes")
        ?? SalahBahazad.Application.Features.Sessions.Commands.AddSessionVideo.AddSessionVideoCommand.MaxBytes
           + (32L * 1024 * 1024);

    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadBytes);
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
        o.MultipartBodyLengthLimit = maxUploadBytes);

    // ── Infrastructure (EF, Firebase, JWT, Redis, Hangfire, Auth) ───────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── SignalR (QuizHub) + Redis backplane (NFR-SCAL-002) ──────────────────
    // The backplane is added only when Redis is configured (dev/prod/tests); a single instance still works
    // without it. Hub JWT (access_token on the /hubs/quiz path) is configured in AddInfrastructure.
    var signalR = builder.Services.AddSignalR();
    var redisConnectionString = builder.Configuration.GetConnectionString("redis");
    if (!string.IsNullOrWhiteSpace(redisConnectionString))
        signalR.AddStackExchangeRedis(redisConnectionString);

    // ── CQRS — source-generated Mediator ────────────────────────────────────
    // Handlers are Scoped so they can consume scoped services (e.g. IAppDbContext / EF Core).
    builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

    // ── FluentValidation pipeline ────────────────────────────────────────────
    // includeInternalTypes: validators are internal sealed, so FluentValidation must be told to
    // register them (default scans public types only — otherwise the validation pipeline silently no-ops).
    builder.Services.AddValidatorsFromAssembly(
        typeof(SalahBahazad.Application.Features.Auth.PermissionCatalog).Assembly,
        includeInternalTypes: true);

    builder.Services.AddTransient(
        typeof(IPipelineBehavior<,>),
        typeof(LoggingBehavior<,>));

    builder.Services.AddTransient(
        typeof(IPipelineBehavior<,>),
        typeof(ValidationBehavior<,>));

    // Transaction scope (innermost behaviour: validation runs before a transaction is opened).
    // Only wraps ITransactionalRequest commands; domain events dispatch after the commit.
    builder.Services.AddTransient(
        typeof(IPipelineBehavior<,>),
        typeof(TransactionBehavior<,>));

    // ── ProblemDetails (RFC 7807) + global exception → HTTP status mapping ────
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // ── JSON: serialize enums as names so role/permissions match the Angular contract ──
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

    // ── Endpoints (auto-discovered IEndpointGroup implementations) ────────────
    app.MapEndpoints();

    // ── SignalR hubs ──────────────────────────────────────────────────────────
    // JWT-authenticated (access_token query scoped to this path, validated in AddInfrastructure); the hub
    // forfeits the active attempt on disconnect (FR-PLAT-QZ-004).
    app.MapHub<SalahBahazad.Api.Hubs.QuizHub>("/hubs/quiz");

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

        // Ensure the local MinIO private bucket exists (dev only; staging/prod buckets are
        // pre-created in Cloudflare R2). A failure is logged but never blocks startup.
        try
        {
            await SalahBahazad.Infrastructure.Services.ObjectStorageInitializer.EnsureBucketsAsync(app.Services);
        }
        catch (Exception storageEx)
        {
            Log.Warning(storageEx, "Object storage bucket bootstrap failed (continuing startup)");
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
