using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Infrastructure.Persistence.Interceptors;

namespace SalahBahazad.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so the EF Core CLI (migrations / database update) can build
/// <see cref="AppDbContext"/> WITHOUT the Aspire AppHost. At runtime Aspire injects
/// <c>ConnectionStrings__DefaultConnection</c> via service discovery; at design time we read that
/// env var if present and otherwise fall back to the AppHost's fixed local-dev value
/// (Postgres on :5432, password "postgres", database "DefaultConnection").
///
/// Usage (Postgres must be reachable, e.g. the AppHost is running):
///   dotnet ef database update --project src/SalahBahazad.Infrastructure --startup-project src/SalahBahazad.Infrastructure
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=DefaultConnection;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        // Migrations only need the model + a connection; the tenant/user resolvers, audit
        // interceptor, and event publisher are never exercised by DDL, so design-time stubs suffice.
        var tenantResolver = new DesignTimeTenantResolver();
        var auditInterceptor = new AuditSaveChangesInterceptor(
            new DesignTimeUserResolver(), tenantResolver, new DesignTimeAuditContext(), TimeProvider.System);

        return new AppDbContext(options, tenantResolver, auditInterceptor, new DesignTimePublisher());
    }

    private sealed class DesignTimeTenantResolver : ICurrentTenantResolver
    {
        public Guid TenantId => Guid.Empty;
        public bool IsResolved => false;
    }

    private sealed class DesignTimeUserResolver : ICurrentUserResolver
    {
        public Guid UserId => Guid.Empty;
        public Guid TenantId => Guid.Empty;
        public StaffRole Role => StaffRole.None;
        public string? DeviceId => null;
        public bool IsAuthenticated => false;
    }

    private sealed class DesignTimeAuditContext : IAuditContextAccessor
    {
        public string? IpAddress => null;
        public string? Portal => null;
    }

    private sealed class DesignTimePublisher : IPublisher
    {
        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => ValueTask.CompletedTask;

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
