using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container (Testcontainers) with Firebase
/// disabled and replaced by a fake. Mints platform JWTs so protected endpoints can be exercised
/// without a real IdP. Shared across the integration suite via <see cref="ApiCollection"/>.
/// </summary>
public sealed class SalahBahazadApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // The test signing material must match what the JWT bearer validation is configured with below.
    public const string JwtSecret = "INTEGRATION-TESTS-ONLY-signing-key-minimum-32-characters!!";
    public const string JwtIssuer = "salah-bahzad-api";
    public const string JwtAudience = "salah-bahzad-admin";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Touching Services builds the host (running ConfigureWebHost, which needs the started
        // container's connection string), then we apply the real migrations.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("Firebase:Disable", "true");
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFirebaseAuthService>();
            services.AddSingleton<IFirebaseAuthService, FakeFirebaseAuthService>();
        });
    }

    // ── Auth helpers ─────────────────────────────────────────────────────────
    public string CreateToken(StaffRole role, Guid tenantId, Guid staffId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staffId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role.ToString()),
            new("token_type", "access"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public HttpClient CreateClientFor(StaffRole role, Guid tenantId, Guid? staffId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(role, tenantId, staffId ?? Guid.NewGuid()));
        return client;
    }

    // ── Seeding helpers ──────────────────────────────────────────────────────
    // No HTTP context in these scopes → the tenant resolver yields Guid.Empty, so the audit
    // interceptor is a no-op and seeding never writes spurious AuditEntry rows.
    public async Task<Guid> SeedTenantAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = Tenant.Create($"Tenant {Guid.NewGuid():N}", $"t-{Guid.NewGuid():N}");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    public async Task<Staff> SeedStaffAsync(Guid tenantId, StaffRole role, string? email = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staff = Staff.Create(
            tenantId,
            $"uid-{Guid.NewGuid():N}",
            "Seeded Staff",
            email ?? $"seed-{Guid.NewGuid():N}@example.com",
            role);
        db.Staff.Add(staff);
        await db.SaveChangesAsync();
        return staff;
    }

    public async Task<AuditEntry?> LatestStaffAuditAsync(Guid tenantId, string action)
        => await LatestAuditAsync(tenantId, nameof(Staff), action);

    /// <summary>Most recent audit entry of a given entity type + action within a tenant (most recent Id wins).</summary>
    public async Task<AuditEntry?> LatestAuditAsync(Guid tenantId, string entityType, string action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditEntries
            .Where(a => a.TenantId == tenantId && a.EntityType == entityType && a.Action == action)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
    }
}
