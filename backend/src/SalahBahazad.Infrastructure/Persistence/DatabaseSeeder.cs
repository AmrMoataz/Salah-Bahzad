using FirebaseAdmin.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Infrastructure.Persistence;

/// <summary>
/// Idempotent bootstrap seeder. Ensures a default <see cref="Tenant"/>, a Firebase
/// auth user, and a linked <see cref="StaffRole.Teacher"/> <see cref="Staff"/> row all exist,
/// linked by Firebase UID. Safe to run repeatedly — every step is an "ensure", so a
/// half-finished run self-heals on the next start.
///
/// The Firebase user is created in whichever Firebase project the running environment's
/// Admin credentials point at, so this is environment-agnostic (Development seeds the
/// Development project, Staging seeds the Staging project, …).
///
/// Bootstrap credentials come from configuration (<c>Seed:*</c>) — never hardcoded. If the
/// teacher email/password are not configured, seeding is a no-op.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        var tenantName = config["Seed:Tenant:Name"] ?? "Salah Bahzad";
        var tenantSlug = (config["Seed:Tenant:Slug"] ?? "salah-bahzad").Trim().ToLowerInvariant();
        var displayName = config["Seed:Teacher:DisplayName"] ?? "Head Teacher";
        var email = config["Seed:Teacher:Email"]?.Trim().ToLowerInvariant();
        var password = config["Seed:Teacher:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Database seed skipped: Seed:Teacher:Email / Seed:Teacher:Password are not configured.");
            return;
        }

        // 1. Tenant (Tenant is the root — it has no tenant query filter).
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == tenantSlug, ct);
        if (tenant is null)
        {
            tenant = Tenant.Create(tenantName, tenantSlug);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded tenant {Slug} ({TenantId}).", tenant.Slug, tenant.Id);
        }

        // 2. Firebase auth user (ensure). The platform stores no password — Firebase owns it.
        var auth = FirebaseAuth.DefaultInstance;
        UserRecord fbUser;
        try
        {
            fbUser = await auth.GetUserByEmailAsync(email, ct);
            logger.LogInformation(
                "Firebase user already exists for {Email} (uid {Uid}).", email, fbUser.Uid);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            fbUser = await auth.CreateUserAsync(
                new UserRecordArgs
                {
                    Email = email,
                    Password = password,
                    DisplayName = displayName,
                    EmailVerified = true,
                },
                ct);
            logger.LogInformation("Created Firebase user for {Email} (uid {Uid}).", email, fbUser.Uid);
        }

        // 3. Staff row (ensure). Bypass the tenant query filter: there is no HTTP context
        //    during seeding, so CurrentTenantResolver yields Guid.Empty and a filtered query
        //    would never see the real row.
        var staffExists = await db.Staff
            .IgnoreQueryFilters()
            .AnyAsync(
                s => s.FirebaseUid == fbUser.Uid
                     || (s.TenantId == tenant.Id && s.Email == email),
                ct);

        if (staffExists)
        {
            logger.LogInformation("Teacher staff already present for {Email} — seed is a no-op.", email);
            return;
        }

        var staff = Staff.Create(tenant.Id, fbUser.Uid, displayName, email, StaffRole.Teacher);
        db.Staff.Add(staff);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded Teacher {Email} (uid {Uid}) in tenant {Slug}.", email, fbUser.Uid, tenant.Slug);
    }
}
