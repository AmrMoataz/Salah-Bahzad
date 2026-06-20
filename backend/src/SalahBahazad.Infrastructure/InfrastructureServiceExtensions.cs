using Amazon.Runtime;
using Amazon.S3;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Infrastructure.Persistence;
using SalahBahazad.Infrastructure.Persistence.Interceptors;
using SalahBahazad.Infrastructure.Services;
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

        // Scoped resolvers
        services.AddScoped<ICurrentTenantResolver, CurrentTenantResolver>();
        services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();
        services.AddScoped<IAuditContextAccessor, AuditContextAccessor>();

        // Audit interceptor (scoped — needs resolver)
        services.AddScoped<AuditSaveChangesInterceptor>();

        // Explicit audit writer for read-access / anonymous entries the interceptor cannot capture.
        services.AddScoped<IAuditWriter, AuditWriter>();

        // Video transcode seam — stubbed in Phase 3 (marks Ready); Hangfire + HLS is Phase 5.
        services.AddScoped<IVideoProcessingQueue, StubVideoProcessingQueue>();

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
            });

        services.AddAuthorization();

        // HybridCache (in-process L1 only for now; wire Redis L2 when the caching phase arrives)
        services.AddHybridCache();

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
