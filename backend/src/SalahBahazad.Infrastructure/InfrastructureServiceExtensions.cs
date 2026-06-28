using Amazon.Runtime;
using Amazon.S3;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Infrastructure.Persistence;
using SalahBahazad.Infrastructure.Persistence.Interceptors;
using SalahBahazad.Infrastructure.Services;
using StackExchange.Redis;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SalahBahazad.Infrastructure;

/// <summary>Registers all infrastructure services into the DI container.</summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // System clock — inject TimeProvider for testable, deterministic time (dotnet-claude-kit convention).
        services.AddSingleton(TimeProvider.System);

        // HTTP context accessors (needed by tenant/user resolvers)
        services.AddHttpContextAccessor();

        // Ambient System-operation context — lets Hangfire jobs / hub callbacks (no HttpContext) resolve the
        // tenant and attribute writes to System (FR-PLAT-QZ-004/005, FR-PLAT-AUD-005). Read by the resolvers.
        services.AddSingleton<ISystemOperationContext, SystemOperationContext>();

        // Scoped resolvers
        services.AddScoped<ICurrentTenantResolver, CurrentTenantResolver>();
        services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();
        services.AddScoped<IAuditContextAccessor, AuditContextAccessor>();

        // Quiz engine system seams — the authoritative timer (Hangfire) and the System-attributed terminal ops.
        services.AddSingleton<IQuizTimerScheduler, HangfireQuizTimerScheduler>();
        services.AddScoped<IQuizLifecycleService, QuizLifecycleService>();
        services.AddScoped<Jobs.QuizAutoSubmitJob>();

        // Audit interceptor (scoped — needs resolver)
        services.AddScoped<AuditSaveChangesInterceptor>();

        // Explicit audit writer for read-access / anonymous entries the interceptor cannot capture.
        services.AddScoped<IAuditWriter, AuditWriter>();

        // Video transcode + secure-playback seams (Phase 5C, FR-PLAT-VID-*). The real Hangfire + ffmpeg HLS
        // pipeline (VideoTranscodeJob) replaces the Phase-3 stub; the handoff store backs the one-time codes.
        services.AddScoped<IVideoProcessingQueue, HangfireVideoProcessingQueue>();
        services.AddScoped<Jobs.VideoTranscodeJob>();
        services.AddSingleton<IMediaTranscoder, FfmpegMediaTranscoder>();
        services.AddSingleton<IPlaybackHandoffStore, RedisPlaybackHandoffStore>();

        // ffmpeg + playback TTL options (env-bound, NFR-SEC-002).
        var transcodeOptions = configuration.GetSection(TranscodeOptions.SectionName).Get<TranscodeOptions>()
            ?? new TranscodeOptions();
        services.AddSingleton(transcodeOptions);

        var playbackOptions = configuration.GetSection(PlaybackOptions.SectionName).Get<PlaybackOptions>()
            ?? new PlaybackOptions();
        services.AddSingleton(playbackOptions);

        // App version floors — hot-reloadable via IOptionsMonitor<AppVersionsOptions> so the operator can
        // raise the min-version floor without a redeploy (NFR-APP-UPD-002, contract §F).
        services.Configure<AppVersionsOptions>(configuration.GetSection(AppVersionsOptions.SectionName));

        // Enrollment side-effect seam — Phase 5B-1 makes this real: snapshots the question bank into a
        // per-student assignment on enrol/extend (idempotent). Prerequisite-quiz snapshot is still 5B-2
        // (FR-PLAT-ENR-005, FR-PLAT-ASG-001, FR-PLAT-QZ-001).
        services.AddScoped<IEnrollmentSideEffects, EnrollmentSideEffects>();

        // Synchronous CSV exports (FR-PLAT-COD-002, FR-ADM-ATT-004); stateless.
        services.AddSingleton<ICodeExporter, CsvCodeExporter>();
        services.AddSingleton<IAttendanceExporter, CsvAttendanceExporter>();

        // EF Core + PostgreSQL
        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            var connStr = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

            opts.UseNpgsql(connStr, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Firebase Admin SDK (initialise once). Skippable via Firebase:Disable=true for tests/CI,
        // where IFirebaseAuthService is replaced by a fake and no real credentials are available.
        if (!configuration.GetValue("Firebase:Disable", false) && FirebaseApp.DefaultInstance is null)
        {
            var serviceAccountJson = configuration["Firebase:ServiceAccountJson"];
            GoogleCredential credential;

            if (!string.IsNullOrWhiteSpace(serviceAccountJson))
                credential = GoogleCredential.FromJson(serviceAccountJson);
            else
                credential = GoogleCredential.GetApplicationDefault();

            FirebaseApp.Create(new AppOptions { Credential = credential });
        }

        // IHttpClientFactory for the Identity Toolkit REST call (password-reset email delivery).
        services.AddHttpClient();

        // Web API key lets Firebase send its own templated reset email (accounts:sendOobCode).
        // Distinct from the Admin SDK service-account credential above. Required unless Firebase is
        // disabled (tests/CI), where IFirebaseAuthService is replaced by a fake and the key is unused.
        var firebaseDisabled = configuration.GetValue("Firebase:Disable", false);
        var firebaseWebApiKey = configuration["Firebase:WebApiKey"];
        if (string.IsNullOrWhiteSpace(firebaseWebApiKey))
        {
            firebaseWebApiKey = firebaseDisabled
                ? string.Empty
                : throw new InvalidOperationException("Firebase:WebApiKey is not configured.");
        }

        services.AddSingleton<IFirebaseAuthService>(sp =>
            new FirebaseAuthService(
                sp.GetRequiredService<IHttpClientFactory>(),
                firebaseWebApiKey));

        // Platform JWT
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Device-binding token service (FR-PLAT-DEV-005) — HMAC over Device:SigningKey (or Jwt:Secret).
        services.AddSingleton<IDeviceBindingService, DeviceBindingService>();

        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };

                // SignalR can't set an Authorization header on the WebSocket/SSE handshake, so the QuizHub passes
                // the platform JWT in the access_token query. Read it ONLY for the hub path and let it flow through
                // the SAME full JWT validation above — not the legacy insecure query-string-credentials scheme
                // (NFR-SEC-005; issue #6 done right).
                opts.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/quiz"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();

        // ── Redis (backplane + L2 + connection map) ─────────────────────────────
        // Aspire injects ConnectionStrings__redis. When present: a shared multiplexer (SignalR backplane +
        // the QuizHub's connection↔attempt map, NFR-SCAL-002/FR-PLAT-QZ-004) and the HybridCache L2 distributed
        // cache. Absent (e.g. a unit-only host): HybridCache stays L1-only and the timer falls back to its
        // idempotent job with no cancellation map.
        var redisConnectionString = configuration.GetConnectionString("redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
        }

        // HybridCache (L1 in-process; promotes to the Redis L2 above when an IDistributedCache is registered).
        services.AddHybridCache();

        // Student-Home weekly-plan cache invalidation seam (contract §D) over the HybridCache above — its tag
        // (plan:{studentId}) is dropped on every student state-change so the plan reflects it on the next read.
        services.AddScoped<IStudentPlanCache, StudentPlanCache>();

        // ── Hangfire (authoritative quiz auto-submit timer, FR-PLAT-QZ-005) ──────
        // PostgreSQL-backed so a scheduled auto-submit survives an API restart; the schema is created on first
        // use (PrepareSchemaIfNecessary). A short polling interval keeps the deadline tight.
        var hangfireConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(hangfireConnectionString)));
        services.AddHangfireServer(opts => opts.SchedulePollingInterval = TimeSpan.FromSeconds(1));

        // ── Object storage (R2 / MinIO) ─────────────────────────────────────────
        AddObjectStorage(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers the object-storage seam from the <c>R2</c> configuration section (env only,
    /// NFR-SEC-002). When configured (dev gets MinIO from the AppHost; staging/prod get real R2
    /// credentials), wires a single S3 client + <see cref="R2FileStorage"/>; otherwise registers a
    /// throwing stub so the app still boots (e.g. integration tests that don't touch storage).
    /// </summary>
    private static void AddObjectStorage(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(R2Options.SectionName).Get<R2Options>() ?? new R2Options();
        services.AddSingleton(options);

        if (!options.IsConfigured)
        {
            services.AddSingleton<IFileStorage, UnconfiguredFileStorage>();
            return;
        }

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                // Path-style addressing is required for MinIO and R2 custom endpoints (no virtual-host
                // bucket DNS). R2 expects region "auto".
                ForcePathStyle = true,
                AuthenticationRegion = "auto",
                // SDK v4 enables additional integrity checksums by default that R2 / older MinIO reject;
                // restrict them to when the operation requires it, matching S3-compatible behaviour.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };

            var credentials = new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey);
            return new AmazonS3Client(credentials, config);
        });

        services.AddSingleton<IFileStorage, R2FileStorage>();
    }
}
