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

        return services;
    }
}
