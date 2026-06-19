using System.Linq.Expressions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Infrastructure.Persistence.Interceptors;

namespace SalahBahazad.Infrastructure.Persistence;

/// <summary>
/// Single EF Core DbContext for the platform.
/// Global query filters enforce tenant isolation (FR-PLAT-TEN-001/003)
/// and soft-delete visibility automatically — no per-handler Where clauses.
/// The filters close over `this` so EF Core evaluates them per-query, not once at model-build time.
/// After a successful commit, buffered domain events are dispatched (handlers see committed state).
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenantResolver tenantResolver,
    AuditSaveChangesInterceptor auditInterceptor,
    IPublisher publisher)
    : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Taxonomy (tenant-owned) — FR-PLAT-TAX-001
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Specialization> Specializations => Set<Specialization>();

    // Location reference data (global, seeded) — FR-PLAT-TAX-003
    public DbSet<City> Cities => Set<City>();
    public DbSet<Region> Regions => Set<Region>();

    /// <summary>
    /// Current tenant for the global query filter. The filter references THIS context property (not the
    /// resolver directly): EF Core caches the model once per context type, but rebinds context-instance
    /// references in a query filter to the <b>current executing context</b> per query — so each request's
    /// own scoped resolver is read, never a stale captured one. Referencing the scoped service directly
    /// (Expression.Constant(resolver)) instead let EF bake the first request's tenant into the cached
    /// query plan and leak it across tenants (NFR-SEC-010).
    /// </summary>
    public Guid CurrentTenantId => tenantResolver.TenantId;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        // Without an ambient transaction, this save IS the commit — dispatch now.
        // Inside a transaction (ITransactionalRequest), ExecuteInTransactionAsync dispatches post-commit.
        if (Database.CurrentTransaction is null)
            await DispatchDomainEventsAsync(cancellationToken);

        return result;
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        var result = await action();

        await transaction.CommitAsync(cancellationToken);

        // Events fire only after the transaction commits, so handlers observe durable state.
        await DispatchDomainEventsAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Publishes (and clears) every buffered <see cref="IDomainEvent"/> on tracked entities.
    /// Handlers run via Mediator's <see cref="IPublisher"/>; a handler that writes triggers another
    /// SaveChanges, re-entering this method — already-cleared buffers make that a safe no-op.
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = ChangeTracker
            .Entries<EntityBase>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (entitiesWithEvents.Count == 0)
            return;

        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
        entitiesWithEvents.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, cancellationToken);
    }

    /// <summary>
    /// Applies tenant-isolation and soft-delete query filters by convention to every entity
    /// implementing <see cref="ITenantOwned"/> / <see cref="ISoftDeletable"/> — so a new tenant
    /// entity is filtered automatically, with no per-entity wiring (backend/CLAUDE.md, FR-PLAT-TEN-*).
    /// The tenant comparison closes over <c>tenantResolver</c> so EF re-evaluates TenantId per query.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isTenantOwned = typeof(ITenantOwned).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantOwned && !isSoftDeletable)
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? body = null;

            if (isTenantOwned)
            {
                // e.TenantId == this.CurrentTenantId. Referencing the context property (not the resolver
                // constant) lets EF rebind to the current context per query, so the tenant is re-read each
                // execution rather than baked into the cached query plan (see CurrentTenantId remarks).
                var entityTenant = Expression.Property(parameter, nameof(ITenantOwned.TenantId));
                var contextTenant = Expression.Property(
                    Expression.Constant(this), nameof(CurrentTenantId));
                body = Expression.Equal(entityTenant, contextTenant);
            }

            if (isSoftDeletable)
            {
                // && !e.IsDeleted
                var notDeleted = Expression.Not(
                    Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));
                body = body is null ? notDeleted : Expression.AndAlso(body, notDeleted);
            }

            var filter = Expression.Lambda(body!, parameter);
            modelBuilder.Entity(clrType).HasQueryFilter(filter);
        }
    }
}
