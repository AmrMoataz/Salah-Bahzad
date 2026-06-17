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
        // HTTP context accessors (needed by tenant/user resolvers)
        services.AddHttpContextAccessor();

        // Scoped resolvers
        services.AddScoped<ICurrentTenantResolver, CurrentTenantResolver>();
        services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();

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

        // Firebase Admin SDK (initialise once)
        if (FirebaseApp.DefaultInstance is null)
        {
            var serviceAccountJson = configuration["Firebase:ServiceAccountJson"];
            GoogleCredential credential;

            if (!string.IsNullOrWhiteSpace(serviceAccountJson))
                credential = GoogleCredential.FromJson(serviceAccountJson);
            else
                credential = GoogleCredential.GetApplicationDefault();

            FirebaseApp.Create(new AppOptions { Credential = credential });
        }

        services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();

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
