using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Infrastructure.Persistence.Interceptors;

namespace SalahBahazad.Infrastructure.Persistence;

/// <summary>
/// Single EF Core DbContext for the platform.
/// Global query filters enforce tenant isolation (FR-PLAT-TEN-001/003)
/// and soft-delete visibility automatically — no per-handler Where clauses.
/// The filters close over `this` so EF Core evaluates them per-query, not once at model-build time.
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenantResolver tenantResolver,
    AuditSaveChangesInterceptor auditInterceptor)
    : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Close over `tenantResolver` (a field of this DbContext instance, not a captured local value),
        // so EF Core parameterises the filter and re-evaluates TenantId per request.
        modelBuilder.Entity<Staff>()
            .HasQueryFilter(s => s.TenantId == tenantResolver.TenantId && !s.IsDeleted);
    }
}
